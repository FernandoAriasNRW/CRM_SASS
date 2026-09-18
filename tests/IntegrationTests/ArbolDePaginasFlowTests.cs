using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// El árbol de páginas de un documento: crear, anidar, mover y renombrar el documento.
///
/// <c>Page.ParentPageId</c> y <c>Page.Order</c> estaban en el dominio desde el primer día y sólo se
/// escribían al crear la página. No había forma de mover nada y la pantalla no llamaba ni siquiera
/// a crear, así que cada documento se quedaba con la estructura exacta que le dejó la plantilla.
///
/// Renombrar un documento tampoco existía: el módulo publicaba borrar documento, borrar página y
/// actualizar página, y nada más. El campo de título de la pantalla escribía en la página activa.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ArbolDePaginasFlowTests(CrmApiFactory factory)
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

    private static async Task<Guid> CrearDocumentoAsync(HttpClient cliente, string titulo)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/docs/", new
        {
            Title = titulo,
            Description = "Creado por las pruebas del árbol",
            Type = 1
        });
        respuesta.EnsureSuccessStatusCode();

        return Guid.Parse((await respuesta.Content.ReadAsStringAsync()).Trim('"'));
    }

    private static async Task<Guid> CrearPaginaAsync(HttpClient cliente, Guid docId, string titulo, Guid? padre = null)
    {
        var respuesta = await cliente.PostAsJsonAsync($"/api/v1/docs/{docId}/pages",
            new { ParentPageId = padre, Title = titulo });
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());

        return Guid.Parse((await respuesta.Content.ReadAsStringAsync()).Trim('"'));
    }

    private static async Task<List<JsonElement>> PaginasAsync(HttpClient cliente, Guid docId)
    {
        var respuesta = await cliente.GetAsync($"/api/v1/docs/{docId}/pages");
        respuesta.EnsureSuccessStatusCode();

        var paginas = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return paginas.EnumerateArray().ToList();
    }

    private static Task<HttpResponseMessage> MoverAsync(HttpClient cliente, Guid pageId, Guid? padre, int orden)
        => cliente.PutAsJsonAsync($"/api/v1/docs/pages/{pageId}/move",
            new { ParentPageId = padre, Order = orden });

    /// <summary>Una subpágina queda colgada de su madre, no del documento.</summary>
    [Fact]
    public async Task Una_subpagina_cuelga_de_su_madre()
    {
        var cliente = await AutenticarAsync();
        var docId = await CrearDocumentoAsync(cliente, "Documento con subpáginas");

        var madre = await CrearPaginaAsync(cliente, docId, "Capítulo primero");
        var hija = await CrearPaginaAsync(cliente, docId, "Apartado", madre);

        var paginas = await PaginasAsync(cliente, docId);
        var laHija = paginas.Single(p => p.GetProperty("id").GetGuid() == hija);

        laHija.GetProperty("parentPageId").GetGuid().Should().Be(madre);
    }

    /// <summary>
    /// Mover una página cambia el orden de verdad, y las hermanas se recolocan.
    ///
    /// Se comprueba el orden de las tres, no sólo el de la que se movió: el servidor renumera el
    /// grupo entero, y una versión que sólo guardara el número pedido dejaría dos páginas con el
    /// mismo orden y el listado se ordenaría por lo que decidiera la base.
    /// </summary>
    [Fact]
    public async Task Mover_una_pagina_recoloca_a_sus_hermanas()
    {
        var cliente = await AutenticarAsync();
        var docId = await CrearDocumentoAsync(cliente, "Documento para reordenar");

        // Crear el documento ya deja una página dentro, así que la de fábrica cuenta como hermana
        // y el orden se mide sobre las cuatro. Dar por hecho que el documento nace vacío es
        // exactamente el tipo de suposición que hace fallar una prueba sin que el código esté mal.
        var deFabrica = (await PaginasAsync(cliente, docId)).Single().GetProperty("id").GetGuid();

        var primera = await CrearPaginaAsync(cliente, docId, "Primera");
        var segunda = await CrearPaginaAsync(cliente, docId, "Segunda");
        var tercera = await CrearPaginaAsync(cliente, docId, "Tercera");

        var movida = await MoverAsync(cliente, tercera, null, 0);
        movida.StatusCode.Should().Be(HttpStatusCode.NoContent, await movida.Content.ReadAsStringAsync());

        var paginas = await PaginasAsync(cliente, docId);
        var porOrden = paginas
            .OrderBy(p => p.GetProperty("order").GetInt32())
            .Select(p => p.GetProperty("id").GetGuid())
            .ToList();

        porOrden.Should().Equal(tercera, deFabrica, primera, segunda);

        // Sin huecos ni empates: si el servidor guardara sólo el número pedido, dos páginas
        // acabarían con el mismo orden y el listado dependería de lo que decidiera la base.
        paginas.Select(p => p.GetProperty("order").GetInt32()).OrderBy(o => o)
            .Should().Equal(new[] { 0, 1, 2, 3 });
    }

    /// <summary>
    /// Sacar una página de un grupo deja al grupo sin huecos.
    ///
    /// El manejador decía en un comentario que renumeraba el origen y no lo hacía: al sacar una de
    /// tres hermanas las otras se quedaban con órdenes salteados, y el siguiente «ponla en la
    /// posición 1» caía en un sitio distinto del que se veía en la barra lateral.
    /// </summary>
    [Fact]
    public async Task Sacar_una_pagina_de_su_grupo_renumera_a_las_que_quedan()
    {
        var cliente = await AutenticarAsync();
        var docId = await CrearDocumentoAsync(cliente, "Documento para sacar páginas");

        var deFabrica = (await PaginasAsync(cliente, docId)).Single().GetProperty("id").GetGuid();
        var primera = await CrearPaginaAsync(cliente, docId, "Primera");
        var segunda = await CrearPaginaAsync(cliente, docId, "Segunda");
        var tercera = await CrearPaginaAsync(cliente, docId, "Tercera");

        var movida = await MoverAsync(cliente, primera, segunda, 0);
        movida.StatusCode.Should().Be(HttpStatusCode.NoContent, await movida.Content.ReadAsStringAsync());

        var raiz = (await PaginasAsync(cliente, docId))
            .Where(p => p.GetProperty("parentPageId").ValueKind == JsonValueKind.Null)
            .OrderBy(p => p.GetProperty("order").GetInt32())
            .ToList();

        raiz.Select(p => p.GetProperty("id").GetGuid()).Should().Equal(deFabrica, segunda, tercera);
        raiz.Select(p => p.GetProperty("order").GetInt32()).Should().Equal(new[] { 0, 1, 2 });
    }

    /// <summary>
    /// Una página no puede colgar de una de sus propias hijas.
    ///
    /// Es el movimiento que rompe el árbol: las dos quedarían colgando la una de la otra y ninguna
    /// del documento, así que desaparecerían de la barra lateral sin haberse borrado.
    /// </summary>
    [Fact]
    public async Task Una_pagina_no_puede_colgar_de_su_propia_hija()
    {
        var cliente = await AutenticarAsync();
        var docId = await CrearDocumentoAsync(cliente, "Documento con ciclo por evitar");

        var madre = await CrearPaginaAsync(cliente, docId, "La madre");
        var hija = await CrearPaginaAsync(cliente, docId, "La hija", madre);
        var nieta = await CrearPaginaAsync(cliente, docId, "La nieta", hija);

        var respuesta = await MoverAsync(cliente, madre, nieta, 0);
        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Y lo que importa: que no se haya movido. Comprobar sólo el código dejaría pasar un
        // rechazo que además hubiera guardado el cambio.
        var paginas = await PaginasAsync(cliente, docId);
        var laMadre = paginas.Single(p => p.GetProperty("id").GetGuid() == madre);
        laMadre.GetProperty("parentPageId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>Renombrar el documento lo renombra, y no toca los títulos de sus páginas.</summary>
    [Fact]
    public async Task Renombrar_el_documento_no_toca_sus_paginas()
    {
        var cliente = await AutenticarAsync();
        var docId = await CrearDocumentoAsync(cliente, "Nombre viejo del documento");
        await CrearPaginaAsync(cliente, docId, "Título de la página");

        var respuesta = await cliente.PutAsJsonAsync($"/api/v1/docs/{docId}",
            new { Title = "Nombre nuevo del documento", Description = (string?)null });

        respuesta.StatusCode.Should().Be(HttpStatusCode.NoContent, await respuesta.Content.ReadAsStringAsync());

        var documentos = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/docs/");
        var elDocumento = documentos.EnumerateArray().Single(d => d.GetProperty("id").GetGuid() == docId);
        elDocumento.GetProperty("title").GetString().Should().Be("Nombre nuevo del documento");

        // La descripción no viajaba en la petición: mandarla como null no debe borrarla.
        elDocumento.GetProperty("description").GetString().Should().Be("Creado por las pruebas del árbol");

        var paginas = await PaginasAsync(cliente, docId);
        paginas.Should().Contain(p => p.GetProperty("title").GetString() == "Título de la página");
    }

    /// <summary>Un título vacío se rechaza y el documento se queda como estaba.</summary>
    [Fact]
    public async Task Un_documento_no_se_queda_sin_titulo()
    {
        var cliente = await AutenticarAsync();
        var docId = await CrearDocumentoAsync(cliente, "Este título tiene que sobrevivir");

        var respuesta = await cliente.PutAsJsonAsync($"/api/v1/docs/{docId}",
            new { Title = "   ", Description = (string?)null });

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var documentos = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/docs/");
        var elDocumento = documentos.EnumerateArray().Single(d => d.GetProperty("id").GetGuid() == docId);
        elDocumento.GetProperty("title").GetString().Should().Be("Este título tiene que sobrevivir");
    }
}
