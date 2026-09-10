using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Las menciones dentro de un documento, y la vuelta: que la tarea sepa quién habla de ella.
///
/// <b>La vuelta es el diferencial.</b> El plan lo decía así: «mencionar un ticket dentro de un
/// documento y que el ticket muestre el documento es algo que ClickUp hace a medias». La ida
/// —escribir la mención y que quede un enlace— la resuelve el editor. La vuelta no se puede
/// resolver leyendo el documento: habría que abrir todos los del inquilino y buscar dentro.
///
/// Y por eso la prueba que importa es la que **escribe una mención por la API y luego pregunta
/// desde el otro lado**. Es la misma forma que la de favoritos: comprueba la unión, que es donde
/// estas cosas se rompen sin dar error.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MencionesFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    private async Task<HttpClient> AutenticarAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return cliente;
    }

    /// <summary>Un documento con una página, para escribir dentro.</summary>
    private static async Task<(Guid DocumentoId, Guid PaginaId)> CrearDocumentoAsync(HttpClient cliente)
    {
        var alta = await cliente.PostAsJsonAsync("/api/v1/docs", new
        {
            Title = $"Notas {Guid.NewGuid():N}"[..24],
            Description = "Para probar menciones",
            Type = 0,
            TeamId = (Guid?)null,
            ProjectId = (Guid?)null,
            InitialContent = (string?)null
        });

        alta.StatusCode.Should().Match(c => c == HttpStatusCode.Created || c == HttpStatusCode.OK,
            await alta.Content.ReadAsStringAsync());

        // El endpoint devuelve el identificador **como una cadena suelta**, no envuelto en un
        // objeto. Es lo que hace de verdad, así que es lo que se lee: una prueba que asume otra
        // forma comprueba una API imaginaria.
        var documentoId = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetGuid();

        // Un documento nace con una página, que es donde se escribe.
        var paginas = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/docs/{documentoId}/pages");
        var primera = paginas.EnumerateArray().First();

        return (documentoId, primera.GetProperty("id").GetGuid());
    }

    /// <summary>Escribe el contenido de una página, tal como lo manda el editor.</summary>
    private static async Task GuardarAsync(HttpClient cliente, Guid paginaId, string titulo, string html)
    {
        var respuesta = await cliente.PutAsJsonAsync($"/api/v1/docs/pages/{paginaId}",
            new { Title = titulo, Content = html });

        respuesta.StatusCode.Should().Match(c => c == HttpStatusCode.OK || c == HttpStatusCode.NoContent,
            await respuesta.Content.ReadAsStringAsync());
    }

    private static async Task<JsonElement> QuienMencionaAsync(HttpClient cliente, string tipo, Guid entidadId)
        => await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/docs/menciones/{tipo}/{entidadId}");

    private static async Task<Guid> UnaTareaAsync(HttpClient cliente)
    {
        var lista = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/tasks?pageSize=1");
        var items = lista.GetProperty("items").EnumerateArray().ToList();

        items.Should().NotBeEmpty("el sembrador crea tareas");
        return items[0].GetProperty("id").GetGuid();
    }

    #region La vuelta: el diferencial

    /// <summary>
    /// La prueba que conecta los dos lados: se menciona una tarea en un documento y la tarea la ve.
    ///
    /// Si el formato que escribe el editor y el que lee el servidor divergieran, esto devolvería
    /// una lista vacía **sin dar ningún error** — la clase de fallo que nadie detecta hasta que
    /// alguien pregunta por qué su documento no aparece en ningún sitio.
    /// </summary>
    [Fact]
    public async Task Una_tarea_mencionada_sabe_que_documento_habla_de_ella()
    {
        var cliente = await AutenticarAsync();
        var tareaId = await UnaTareaAsync(cliente);
        var (_, paginaId) = await CrearDocumentoAsync(cliente);

        await GuardarAsync(cliente, paginaId, "Acta de reunión",
            $"""<p>Se acordó avanzar con <span data-mencion-tipo="Tarea" data-mencion-id="{tareaId}">esa tarea</span>.</p>""");

        var quien = await QuienMencionaAsync(cliente, "Tarea", tareaId);

        // Se busca **esta** página, no se exige ser la única que menciona la tarea. Todas las
        // pruebas de esta clase trabajan sobre la misma tarea del sembrador, así que exigir
        // exclusividad las hacía pasar por separado y fallar juntas — el patrón que ya mordió con
        // las automatizaciones sin condiciones.
        var mencion = quien.EnumerateArray()
            .Should().ContainSingle(m => m.GetProperty("pageId").GetGuid() == paginaId).Subject;

        mencion.GetProperty("tituloDeLaPagina").GetString().Should().Be("Acta de reunión");
        mencion.GetProperty("textoVisible").GetString().Should().Be("esa tarea");
    }

    /// <summary>
    /// Borrar la mención del texto la borra de verdad.
    ///
    /// Es la razón de que las menciones se reescriban enteras en cada guardado en vez de ir
    /// añadiendo: con un diferencial, lo que no se detecta se queda para siempre y la tarea
    /// seguiría enseñando un documento que ya no habla de ella.
    /// </summary>
    [Fact]
    public async Task Quitar_la_mencion_del_texto_la_quita_de_la_tarea()
    {
        var cliente = await AutenticarAsync();
        var tareaId = await UnaTareaAsync(cliente);
        var (_, paginaId) = await CrearDocumentoAsync(cliente);

        await GuardarAsync(cliente, paginaId, "Con mención",
            $"""<p><span data-mencion-tipo="Tarea" data-mencion-id="{tareaId}">La tarea</span></p>""");

        (await QuienMencionaAsync(cliente, "Tarea", tareaId)).EnumerateArray()
            .Should().ContainSingle(m => m.GetProperty("pageId").GetGuid() == paginaId);

        await GuardarAsync(cliente, paginaId, "Sin mención", "<p>Ya no se habla de nada.</p>");

        (await QuienMencionaAsync(cliente, "Tarea", tareaId)).EnumerateArray()
            .Should().NotContain(m => m.GetProperty("pageId").GetGuid() == paginaId,
                "la mención se borró del texto, así que la tarea no puede seguir enseñando el documento");
    }

    /// <summary>
    /// El índice se rehace, no se acumula: guardar dos veces no duplica la mención.
    ///
    /// Es lo que pasaría si se insertara sin borrar antes, y la pantalla de la tarea enseñaría el
    /// mismo documento tantas veces como se hubiera guardado.
    /// </summary>
    [Fact]
    public async Task Guardar_varias_veces_no_duplica_la_mencion()
    {
        var cliente = await AutenticarAsync();
        var tareaId = await UnaTareaAsync(cliente);
        var (_, paginaId) = await CrearDocumentoAsync(cliente);

        var html = $"""<p><span data-mencion-tipo="Tarea" data-mencion-id="{tareaId}">Otra vez</span></p>""";

        await GuardarAsync(cliente, paginaId, "Uno", html);
        await GuardarAsync(cliente, paginaId, "Dos", html);
        await GuardarAsync(cliente, paginaId, "Tres", html);

        (await QuienMencionaAsync(cliente, "Tarea", tareaId)).EnumerateArray()
            .Count(m => m.GetProperty("pageId").GetGuid() == paginaId)
            .Should().Be(1);
    }

    /// <summary>Sin nadie que la mencione, la lista es vacía: no es un error, es la respuesta.</summary>
    [Fact]
    public async Task Una_tarea_sin_menciones_devuelve_una_lista_vacia()
    {
        var cliente = await AutenticarAsync();

        var quien = await QuienMencionaAsync(cliente, "Tarea", Guid.NewGuid());

        quien.EnumerateArray().Should().BeEmpty();
    }

    #endregion

    #region Qué se puede mencionar

    [Fact]
    public async Task Se_pueden_mencionar_tickets_y_personas_ademas_de_tareas()
    {
        var cliente = await AutenticarAsync();
        var (_, paginaId) = await CrearDocumentoAsync(cliente);

        var ticketId = Guid.NewGuid();
        var personaId = Guid.NewGuid();

        await GuardarAsync(cliente, paginaId, "Varias",
            $"""
             <p><span data-mencion-tipo="Ticket" data-mencion-id="{ticketId}">Un ticket</span>
             y <span data-mencion-tipo="Persona" data-mencion-id="{personaId}">alguien</span></p>
             """);

        (await QuienMencionaAsync(cliente, "Ticket", ticketId)).EnumerateArray().Should().ContainSingle();
        (await QuienMencionaAsync(cliente, "Persona", personaId)).EnumerateArray().Should().ContainSingle();
    }

    /// <summary>
    /// Un tipo que no se puede mencionar se rechaza diciendo cuáles sí.
    ///
    /// La lista es cerrada a propósito: una mención a un tipo que ninguna pantalla enseña es un
    /// dato que no vuelve a ver nadie.
    /// </summary>
    [Fact]
    public async Task Un_tipo_que_no_se_puede_mencionar_se_rechaza()
    {
        var cliente = await AutenticarAsync();

        var respuesta = await cliente.GetAsync($"/api/v1/docs/menciones/Factura/{Guid.NewGuid()}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var mensaje = await respuesta.Content.ReadAsStringAsync();
        mensaje.Should().Contain("Factura").And.Contain("Tarea");
    }

    /// <summary>Una mención mal formada no se guarda, y el guardado del texto no falla por ello.</summary>
    [Fact]
    public async Task Una_mencion_sin_identificador_no_impide_guardar_la_pagina()
    {
        var cliente = await AutenticarAsync();
        var (documentoId, paginaId) = await CrearDocumentoAsync(cliente);

        await GuardarAsync(cliente, paginaId, "Rota",
            """<p><span data-mencion-tipo="Tarea">Sin identificador</span></p>""");

        // Lo que importa es que el texto de la persona se haya guardado: una mención a medias no
        // puede costarle el contenido.
        var paginas = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/docs/{documentoId}/pages");

        paginas.EnumerateArray()
            .Should().Contain(p => p.GetProperty("title").GetString() == "Rota");
    }

    #endregion

    [Fact]
    public async Task Sin_autenticar_no_se_consultan_menciones()
    {
        var anonimo = factory.CreateClient();

        (await anonimo.GetAsync($"/api/v1/docs/menciones/Tarea/{Guid.NewGuid()}")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }
}
