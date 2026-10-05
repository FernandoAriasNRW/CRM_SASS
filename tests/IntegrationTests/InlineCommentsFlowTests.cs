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
public sealed class InlineCommentsFlowTests(CrmApiFactory factory)
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

    private static async Task<(Guid document, Guid page)> CreateDocumentWithPageAsync(HttpClient client)
    {
        var created = await client.PostAsJsonAsync("/api/v1/docs/", new
        {
            Title = "Documento para comentar",
            Description = "De las pruebas de comentarios en línea",
            Type = 1
        });
        created.EnsureSuccessStatusCode();

        var document = Guid.Parse((await created.Content.ReadAsStringAsync()).Trim('"'));

        var pages = await client.GetFromJsonAsync<JsonElement>($"/api/v1/docs/{document}/pages");
        var page = pages.EnumerateArray().First().GetProperty("id").GetGuid();

        return (document, page);
    }

    private static async Task<Guid> AnnotateAsync(HttpClient client, Guid page, string quoted)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/docs/pages/{page}/annotations", new { QuotedText = quoted });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return Guid.Parse((await response.Content.ReadAsStringAsync()).Trim('"'));
    }

    private static async Task<List<JsonElement>> AnnotationsAsync(HttpClient client, Guid page)
    {
        var response = await client.GetAsync($"/api/v1/docs/pages/{page}/annotations");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
    }

    /// <summary>
    /// Una anotación recuerda sobre qué se comentó, y admite un hilo por el módulo de comentarios.
    /// </summary>
    [Fact]
    public async Task An_annotation_keeps_the_quote_and_takes_a_thread()
    {
        var client = await AuthenticateAsync();
        var (document, page) = await CreateDocumentWithPageAsync(client);

        var anotacion = await AnnotateAsync(client, page, "esta frase hay que revisarla");

        var annotations = await AnnotationsAsync(client, page);
        var ours = annotations.Single(a => a.GetProperty("id").GetGuid() == anotacion);

        ours.GetProperty("quotedText").GetString().Should().Be("esta frase hay que revisarla");
        ours.GetProperty("documentId").GetGuid().Should().Be(document,
            "el documento se saca de la página, no de lo que mande el cliente");
        ours.GetProperty("resolvedAtUtc").ValueKind.Should().Be(JsonValueKind.Null);

        // El hilo va por Comments, con la anotación como entidad comentada. Si esto no funcionara,
        // la anotación sería una marca de color sin conversación detrás.
        var comment = await client.PostAsJsonAsync(
            $"/api/v1/comments/Annotation/{anotacion}", new { Text = "Yo lo diría de otra forma" });

        comment.IsSuccessStatusCode.Should().BeTrue(await comment.Content.ReadAsStringAsync());

        var thread = await client.GetFromJsonAsync<JsonElement>($"/api/v1/comments/Annotation/{anotacion}");
        thread.EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("text").GetString().Should().Be("Yo lo diría de otra forma");
    }

    /// <summary>
    /// Resolver no borra: la anotación se queda, marcada, y se puede volver a abrir.
    ///
    /// Un comentario que desaparece al darlo por atendido se lleva por delante el motivo del
    /// cambio, que semanas después es justo lo que se busca.
    /// </summary>
    [Fact]
    public async Task Resolving_does_not_delete_and_can_be_reopened()
    {
        var client = await AuthenticateAsync();
        var (_, page) = await CreateDocumentWithPageAsync(client);
        var anotacion = await AnnotateAsync(client, page, "un párrafo cualquiera");

        var resolved = await client.PutAsJsonAsync(
            $"/api/v1/docs/annotations/{anotacion}/resolve", new { IsResolved = true });
        resolved.StatusCode.Should().Be(HttpStatusCode.NoContent, await resolved.Content.ReadAsStringAsync());

        var afterResolve = (await AnnotationsAsync(client, page))
            .Single(a => a.GetProperty("id").GetGuid() == anotacion);

        afterResolve.GetProperty("resolvedAtUtc").ValueKind.Should().NotBe(JsonValueKind.Null,
            "sigue ahí, marcada: resolver no es borrar");

        await client.PutAsJsonAsync($"/api/v1/docs/annotations/{anotacion}/resolve", new { IsResolved = false });

        var afterReopen = (await AnnotationsAsync(client, page))
            .Single(a => a.GetProperty("id").GetGuid() == anotacion);

        afterReopen.GetProperty("resolvedAtUtc").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>Borrar la anotación la quita del listado de la página.</summary>
    [Fact]
    public async Task Deleting_an_annotation_removes_it_from_the_page()
    {
        var client = await AuthenticateAsync();
        var (_, page) = await CreateDocumentWithPageAsync(client);

        var survives = await AnnotateAsync(client, page, "esta se queda");
        var goes = await AnnotateAsync(client, page, "esta se va");

        var deleted = await client.DeleteAsync($"/api/v1/docs/annotations/{goes}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var remaining = await AnnotationsAsync(client, page);
        remaining.Select(a => a.GetProperty("id").GetGuid()).Should().Equal(survives);
    }

    /// <summary>
    /// Las anotaciones son de su página, no del documento entero.
    ///
    /// Un documento con varias páginas tiene conversaciones distintas en cada una; mezclarlas
    /// haría que el panel enseñara comentarios sobre texto que no está a la vista.
    /// </summary>
    [Fact]
    public async Task Each_page_sees_only_its_annotations()
    {
        var client = await AuthenticateAsync();
        var (document, first) = await CreateDocumentWithPageAsync(client);

        var created = await client.PostAsJsonAsync($"/api/v1/docs/{document}/pages",
            new { ParentPageId = (Guid?)null, Title = "La segunda página" });
        var second = Guid.Parse((await created.Content.ReadAsStringAsync()).Trim('"'));

        await AnnotateAsync(client, first, "comentario de la primera");
        await AnnotateAsync(client, second, "comentario de la segunda");

        var ofFirst = await AnnotationsAsync(client, first);
        ofFirst.Should().ContainSingle()
            .Which.GetProperty("quotedText").GetString().Should().Be("comentario de la primera");
    }
}
