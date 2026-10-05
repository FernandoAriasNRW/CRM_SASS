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
public sealed class PageTreeFlowTests(CrmApiFactory factory)
{
    private async Task<HttpClient> AuthenticateAsync()
    {
        var login = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { Email = "admin@acme.com", Password = "admin123" });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private static async Task<Guid> CreateDocumentAsync(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync("/api/v1/docs/", new
        {
            Title = title,
            Description = "Creado por las pruebas del árbol",
            Type = 1
        });
        response.EnsureSuccessStatusCode();

        return Guid.Parse((await response.Content.ReadAsStringAsync()).Trim('"'));
    }

    private static async Task<Guid> CreatePageAsync(HttpClient client, Guid docId, string title, Guid? parent = null)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/docs/{docId}/pages",
            new { ParentPageId = parent, Title = title });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return Guid.Parse((await response.Content.ReadAsStringAsync()).Trim('"'));
    }

    private static async Task<List<JsonElement>> PagesAsync(HttpClient client, Guid docId)
    {
        var response = await client.GetAsync($"/api/v1/docs/{docId}/pages");
        response.EnsureSuccessStatusCode();

        var pages = await response.Content.ReadFromJsonAsync<JsonElement>();
        return pages.EnumerateArray().ToList();
    }

    private static Task<HttpResponseMessage> MoveAsync(HttpClient client, Guid pageId, Guid? parent, int order)
        => client.PutAsJsonAsync($"/api/v1/docs/pages/{pageId}/move",
            new { ParentPageId = parent, Order = order });

    /// <summary>Una subpágina queda colgada de su madre, no del documento.</summary>
    [Fact]
    public async Task A_subpage_hangs_from_its_parent()
    {
        var client = await AuthenticateAsync();
        var docId = await CreateDocumentAsync(client, "Documento con subpáginas");

        var parentPage = await CreatePageAsync(client, docId, "Capítulo primero");
        var child = await CreatePageAsync(client, docId, "Apartado", parentPage);

        var pages = await PagesAsync(client, docId);
        var theChild = pages.Single(p => p.GetProperty("id").GetGuid() == child);

        theChild.GetProperty("parentPageId").GetGuid().Should().Be(parentPage);
    }

    /// <summary>
    /// Mover una página cambia el orden de verdad, y las hermanas se recolocan.
    ///
    /// Se comprueba el orden de las tres, no sólo el de la que se movió: el servidor renumera el
    /// grupo entero, y una versión que sólo guardara el número pedido dejaría dos páginas con el
    /// mismo orden y el listado se ordenaría por lo que decidiera la base.
    /// </summary>
    [Fact]
    public async Task Moving_a_page_repositions_its_siblings()
    {
        var client = await AuthenticateAsync();
        var docId = await CreateDocumentAsync(client, "Documento para reordenar");

        // Crear el documento ya deja una página dentro, así que la de fábrica cuenta como hermana
        // y el orden se mide sobre las cuatro. Dar por hecho que el documento nace vacío es
        // exactamente el tipo de suposición que hace fallar una prueba sin que el código esté mal.
        var builtIn = (await PagesAsync(client, docId)).Single().GetProperty("id").GetGuid();

        var first = await CreatePageAsync(client, docId, "Primera");
        var second = await CreatePageAsync(client, docId, "Segunda");
        var third = await CreatePageAsync(client, docId, "Tercera");

        var moved = await MoveAsync(client, third, null, 0);
        moved.StatusCode.Should().Be(HttpStatusCode.NoContent, await moved.Content.ReadAsStringAsync());

        var pages = await PagesAsync(client, docId);
        var byOrder = pages
            .OrderBy(p => p.GetProperty("order").GetInt32())
            .Select(p => p.GetProperty("id").GetGuid())
            .ToList();

        byOrder.Should().Equal(third, builtIn, first, second);

        // Sin huecos ni empates: si el servidor guardara sólo el número pedido, dos páginas
        // acabarían con el mismo orden y el listado dependería de lo que decidiera la base.
        pages.Select(p => p.GetProperty("order").GetInt32()).OrderBy(o => o)
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
    public async Task Moving_a_page_out_renumbers_the_rest()
    {
        var client = await AuthenticateAsync();
        var docId = await CreateDocumentAsync(client, "Documento para sacar páginas");

        var builtIn = (await PagesAsync(client, docId)).Single().GetProperty("id").GetGuid();
        var first = await CreatePageAsync(client, docId, "Primera");
        var second = await CreatePageAsync(client, docId, "Segunda");
        var third = await CreatePageAsync(client, docId, "Tercera");

        var moved = await MoveAsync(client, first, second, 0);
        moved.StatusCode.Should().Be(HttpStatusCode.NoContent, await moved.Content.ReadAsStringAsync());

        var root = (await PagesAsync(client, docId))
            .Where(p => p.GetProperty("parentPageId").ValueKind == JsonValueKind.Null)
            .OrderBy(p => p.GetProperty("order").GetInt32())
            .ToList();

        root.Select(p => p.GetProperty("id").GetGuid()).Should().Equal(builtIn, second, third);
        root.Select(p => p.GetProperty("order").GetInt32()).Should().Equal(new[] { 0, 1, 2 });
    }

    /// <summary>
    /// Una página no puede colgar de una de sus propias hijas.
    ///
    /// Es el movimiento que rompe el árbol: las dos quedarían colgando la una de la otra y ninguna
    /// del documento, así que desaparecerían de la barra lateral sin haberse borrado.
    /// </summary>
    [Fact]
    public async Task A_page_cannot_hang_from_its_own_child()
    {
        var client = await AuthenticateAsync();
        var docId = await CreateDocumentAsync(client, "Documento con ciclo por evitar");

        var parentPage = await CreatePageAsync(client, docId, "La madre");
        var child = await CreatePageAsync(client, docId, "La hija", parentPage);
        var grandchild = await CreatePageAsync(client, docId, "La nieta", child);

        var response = await MoveAsync(client, parentPage, grandchild, 0);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Y lo que importa: que no se haya movido. Comprobar sólo el código dejaría pasar un
        // rechazo que además hubiera guardado el cambio.
        var pages = await PagesAsync(client, docId);
        var theParent = pages.Single(p => p.GetProperty("id").GetGuid() == parentPage);
        theParent.GetProperty("parentPageId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>Renombrar el documento lo renombra, y no toca los títulos de sus páginas.</summary>
    [Fact]
    public async Task Renaming_the_document_keeps_its_pages()
    {
        var client = await AuthenticateAsync();
        var docId = await CreateDocumentAsync(client, "Nombre viejo del documento");
        await CreatePageAsync(client, docId, "Título de la página");

        var response = await client.PutAsJsonAsync($"/api/v1/docs/{docId}",
            new { Title = "Nombre nuevo del documento", Description = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());

        var documents = await client.GetFromJsonAsync<JsonElement>("/api/v1/docs/");
        var theDocument = documents.EnumerateArray().Single(d => d.GetProperty("id").GetGuid() == docId);
        theDocument.GetProperty("title").GetString().Should().Be("Nombre nuevo del documento");

        // La descripción no viajaba en la petición: mandarla como null no debe borrarla.
        theDocument.GetProperty("description").GetString().Should().Be("Creado por las pruebas del árbol");

        var pages = await PagesAsync(client, docId);
        pages.Should().Contain(p => p.GetProperty("title").GetString() == "Título de la página");
    }

    /// <summary>Un título vacío se rechaza y el documento se queda como estaba.</summary>
    [Fact]
    public async Task A_document_cannot_lose_its_title()
    {
        var client = await AuthenticateAsync();
        var docId = await CreateDocumentAsync(client, "Este título tiene que sobrevivir");

        var response = await client.PutAsJsonAsync($"/api/v1/docs/{docId}",
            new { Title = "   ", Description = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var documents = await client.GetFromJsonAsync<JsonElement>("/api/v1/docs/");
        var theDocument = documents.EnumerateArray().Single(d => d.GetProperty("id").GetGuid() == docId);
        theDocument.GetProperty("title").GetString().Should().Be("Este título tiene que sobrevivir");
    }
}
