using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Comentar un trozo de un documento.
///
/// La pieza que faltaba de la Fase 5B. El diseño reparte el trabajo en dos: <b>Docs guarda dónde
/// está pegado</b> el comentario —qué página, qué texto se citó, si está resuelto— y <b>Comments
/// guarda la conversación</b>, con el identificador de la anotación como entidad comentada.
///
/// Por eso la prueba cruza los dos módulos: una anotación sin hilo no es un comentario, y un hilo
/// sin anotación no se puede pintar en ningún sitio. Comprobar sólo una mitad dejaría pasar
/// exactamente el fallo que este proyecto lleva persiguiendo.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ComentariosEnLineaFlowTests(CrmApiFactory factory)
{
    private async Task<HttpClient> AutenticarAsync()
    {
        var login = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { Email = "admin@acme.com", Password = "admin123" });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return cliente;
    }

    private static async Task<(Guid documento, Guid pagina)> CrearDocumentoConPaginaAsync(HttpClient cliente)
    {
        var creado = await cliente.PostAsJsonAsync("/api/v1/docs/", new
        {
            Title = "Documento para comentar",
            Description = "De las pruebas de comentarios en línea",
            Type = 1
        });
        creado.EnsureSuccessStatusCode();

        var documento = Guid.Parse((await creado.Content.ReadAsStringAsync()).Trim('"'));

        var paginas = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/docs/{documento}/pages");
        var pagina = paginas.EnumerateArray().First().GetProperty("id").GetGuid();

        return (documento, pagina);
    }

    private static async Task<Guid> AnotarAsync(HttpClient cliente, Guid pagina, string citado)
    {
        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/v1/docs/pages/{pagina}/anotaciones", new { TextoCitado = citado });

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());
        return Guid.Parse((await respuesta.Content.ReadAsStringAsync()).Trim('"'));
    }

    private static async Task<List<JsonElement>> AnotacionesAsync(HttpClient cliente, Guid pagina)
    {
        var respuesta = await cliente.GetAsync($"/api/v1/docs/pages/{pagina}/anotaciones");
        respuesta.EnsureSuccessStatusCode();

        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
    }

    /// <summary>
    /// Una anotación recuerda sobre qué se comentó, y admite un hilo por el módulo de comentarios.
    /// </summary>
    [Fact]
    public async Task Una_anotacion_guarda_la_cita_y_admite_un_hilo()
    {
        var cliente = await AutenticarAsync();
        var (documento, pagina) = await CrearDocumentoConPaginaAsync(cliente);

        var anotacion = await AnotarAsync(cliente, pagina, "esta frase hay que revisarla");

        var anotaciones = await AnotacionesAsync(cliente, pagina);
        var laNuestra = anotaciones.Single(a => a.GetProperty("id").GetGuid() == anotacion);

        laNuestra.GetProperty("textoCitado").GetString().Should().Be("esta frase hay que revisarla");
        laNuestra.GetProperty("documentId").GetGuid().Should().Be(documento,
            "el documento se saca de la página, no de lo que mande el cliente");
        laNuestra.GetProperty("resueltaUtc").ValueKind.Should().Be(JsonValueKind.Null);

        // El hilo va por Comments, con la anotación como entidad comentada. Si esto no funcionara,
        // la anotación sería una marca de color sin conversación detrás.
        var comentario = await cliente.PostAsJsonAsync(
            $"/api/v1/comments/Anotacion/{anotacion}", new { Texto = "Yo lo diría de otra forma" });

        comentario.IsSuccessStatusCode.Should().BeTrue(await comentario.Content.ReadAsStringAsync());

        var hilo = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/comments/Anotacion/{anotacion}");
        hilo.EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("texto").GetString().Should().Be("Yo lo diría de otra forma");
    }

    /// <summary>
    /// Resolver no borra: la anotación se queda, marcada, y se puede volver a abrir.
    ///
    /// Un comentario que desaparece al darlo por atendido se lleva por delante el motivo del
    /// cambio, que semanas después es justo lo que se busca.
    /// </summary>
    [Fact]
    public async Task Resolver_no_borra_y_se_puede_reabrir()
    {
        var cliente = await AutenticarAsync();
        var (_, pagina) = await CrearDocumentoConPaginaAsync(cliente);
        var anotacion = await AnotarAsync(cliente, pagina, "un párrafo cualquiera");

        var resuelta = await cliente.PutAsJsonAsync(
            $"/api/v1/docs/anotaciones/{anotacion}/resolver", new { Resuelta = true });
        resuelta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resuelta.Content.ReadAsStringAsync());

        var trasResolver = (await AnotacionesAsync(cliente, pagina))
            .Single(a => a.GetProperty("id").GetGuid() == anotacion);

        trasResolver.GetProperty("resueltaUtc").ValueKind.Should().NotBe(JsonValueKind.Null,
            "sigue ahí, marcada: resolver no es borrar");

        await cliente.PutAsJsonAsync($"/api/v1/docs/anotaciones/{anotacion}/resolver", new { Resuelta = false });

        var trasReabrir = (await AnotacionesAsync(cliente, pagina))
            .Single(a => a.GetProperty("id").GetGuid() == anotacion);

        trasReabrir.GetProperty("resueltaUtc").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>Borrar la anotación la quita del listado de la página.</summary>
    [Fact]
    public async Task Borrar_una_anotacion_la_quita_de_la_pagina()
    {
        var cliente = await AutenticarAsync();
        var (_, pagina) = await CrearDocumentoConPaginaAsync(cliente);

        var sobrevive = await AnotarAsync(cliente, pagina, "esta se queda");
        var seVa = await AnotarAsync(cliente, pagina, "esta se va");

        var borrada = await cliente.DeleteAsync($"/api/v1/docs/anotaciones/{seVa}");
        borrada.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var quedan = await AnotacionesAsync(cliente, pagina);
        quedan.Select(a => a.GetProperty("id").GetGuid()).Should().Equal(sobrevive);
    }

    /// <summary>
    /// Las anotaciones son de su página, no del documento entero.
    ///
    /// Un documento con varias páginas tiene conversaciones distintas en cada una; mezclarlas
    /// haría que el panel enseñara comentarios sobre texto que no está a la vista.
    /// </summary>
    [Fact]
    public async Task Cada_pagina_ve_solo_sus_anotaciones()
    {
        var cliente = await AutenticarAsync();
        var (documento, primera) = await CrearDocumentoConPaginaAsync(cliente);

        var creada = await cliente.PostAsJsonAsync($"/api/v1/docs/{documento}/pages",
            new { ParentPageId = (Guid?)null, Title = "La segunda página" });
        var segunda = Guid.Parse((await creada.Content.ReadAsStringAsync()).Trim('"'));

        await AnotarAsync(cliente, primera, "comentario de la primera");
        await AnotarAsync(cliente, segunda, "comentario de la segunda");

        var deLaPrimera = await AnotacionesAsync(cliente, primera);
        deLaPrimera.Should().ContainSingle()
            .Which.GetProperty("textoCitado").GetString().Should().Be("comentario de la primera");
    }
}
