using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Comentarios, de punta a punta contra la API real.
///
/// Existen porque hasta ahora **no existían**: el panel de detalle de tarea llevaba su interfaz
/// de comentarios escrita y `GET /tasks/{id}/comments` devolvía 404 en cada apertura. No es que
/// fallara: es que no había ninguna entidad de comentario en todo el backend.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CommentsFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    private async Task<HttpClient> AuthenticateAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private static async Task<Guid> CommentAsync(HttpClient client, string entity, Guid entityId, string text, Guid? repliesTo = null)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/comments/{entity}/{entityId}", new { text = text, replyToId = repliesTo });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static Task<JsonElement> ThreadAsync(HttpClient client, string entity, Guid entityId) =>
        client.GetFromJsonAsync<JsonElement>($"/api/v1/comments/{entity}/{entityId}");

    [Theory]
    [InlineData("Task")]
    [InlineData("Ticket")]
    [InlineData("Project")]
    public async Task Commenting_and_reading_work_on_the_three_entities(string entity)
    {
        var client = await AuthenticateAsync();
        var entityId = Guid.NewGuid();

        await CommentAsync(client, entity, entityId, "Primer comentario");

        var thread = await ThreadAsync(client, entity, entityId);
        thread.EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("text").GetString().Should().Be("Primer comentario");
    }

    /// <summary>El hilo se lee en orden: del más antiguo al más nuevo.</summary>
    [Fact]
    public async Task The_thread_comes_in_writing_order()
    {
        var client = await AuthenticateAsync();
        var entityId = Guid.NewGuid();

        await CommentAsync(client, "Task", entityId, "Primero");
        await CommentAsync(client, "Task", entityId, "Segundo");

        var texts = (await ThreadAsync(client, "Task", entityId))
            .EnumerateArray().Select(c => c.GetProperty("text").GetString()).ToList();

        texts.Should().Equal("Primero", "Segundo");
    }

    /// <summary>Un hilo vacío es una lista vacía, no un 404. Era justo el defecto que se arregla.</summary>
    [Fact]
    public async Task An_entity_without_comments_returns_an_empty_list()
    {
        var client = await AuthenticateAsync();

        var response = await client.GetAsync($"/api/v1/comments/Task/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task The_author_edits_the_comment_and_it_is_marked_edited()
    {
        var client = await AuthenticateAsync();
        var entityId = Guid.NewGuid();
        var id = await CommentAsync(client, "Task", entityId, "Con errata");

        var edit = await client.PutAsJsonAsync($"/api/v1/comments/{id}", new { text = "Sin errata" });
        edit.StatusCode.Should().Be(HttpStatusCode.OK);

        var comment = (await ThreadAsync(client, "Task", entityId)).EnumerateArray().Single();
        comment.GetProperty("text").GetString().Should().Be("Sin errata");
        comment.GetProperty("editedAtUtc").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task An_empty_comment_is_rejected()
    {
        var client = await AuthenticateAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/comments/Task/{Guid.NewGuid()}", new { text = "   " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Comments_on_entities_nobody_renders_are_rejected()
    {
        var client = await AuthenticateAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/comments/Factura/{Guid.NewGuid()}", new { text = "Hola" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_comment_can_be_replied_to()
    {
        var client = await AuthenticateAsync();
        var entityId = Guid.NewGuid();
        var parent = await CommentAsync(client, "Task", entityId, "Pregunta");

        await CommentAsync(client, "Task", entityId, "Respuesta", parent);

        var thread = await ThreadAsync(client, "Task", entityId);
        thread.GetArrayLength().Should().Be(2);

        // El comentario original tiene `replyToId` nulo, así que hay que mirarlo antes de
        // leerlo como Guid: el hilo trae los dos.
        var responses = thread.EnumerateArray()
            .Select(c => c.GetProperty("replyToId"))
            .Where(r => r.ValueKind != JsonValueKind.Null)
            .Select(r => r.GetGuid())
            .ToList();

        responses.Should().Equal(parent);
    }

    /// <summary>
    /// Un solo nivel, como las subtareas: es lo que evita los hilos que se van a la derecha hasta
    /// no caber, y permite pintarlos con una cuenta en vez de recorriendo un árbol.
    /// </summary>
    [Fact]
    public async Task A_reply_cannot_be_replied_to()
    {
        var client = await AuthenticateAsync();
        var entityId = Guid.NewGuid();
        var parent = await CommentAsync(client, "Task", entityId, "Pregunta");
        var response = await CommentAsync(client, "Task", entityId, "Respuesta", parent);

        var third = await client.PostAsJsonAsync(
            $"/api/v1/comments/Task/{entityId}", new { text = "Otra", replyToId = response });

        third.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Sin esta comprobación, una respuesta podría colgarse de un comentario de otra tarea y
    /// aparecer en un hilo donde nadie la escribió.
    /// </summary>
    [Fact]
    public async Task A_reply_cannot_jump_to_another_thread()
    {
        var client = await AuthenticateAsync();
        var parent = await CommentAsync(client, "Task", Guid.NewGuid(), "En una tarea");

        var intruder = await client.PostAsJsonAsync(
            $"/api/v1/comments/Task/{Guid.NewGuid()}", new { text = "En otra", replyToId = parent });

        intruder.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_comment_can_be_deleted()
    {
        var client = await AuthenticateAsync();
        var entityId = Guid.NewGuid();
        var id = await CommentAsync(client, "Task", entityId, "Para borrar");

        var deleted = await client.DeleteAsync($"/api/v1/comments/{id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ThreadAsync(client, "Task", entityId)).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Editing_a_missing_comment_returns_404()
    {
        var client = await AuthenticateAsync();

        var response = await client.PutAsJsonAsync($"/api/v1/comments/{Guid.NewGuid()}", new { text = "Hola" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>Los comentarios de una entidad no se mezclan con los de otra.</summary>
    [Fact]
    public async Task Each_entity_has_its_own_thread()
    {
        var client = await AuthenticateAsync();
        var aTask = Guid.NewGuid();
        var otherTask = Guid.NewGuid();

        await CommentAsync(client, "Task", aTask, "De la primera");
        await CommentAsync(client, "Task", otherTask, "De la segunda");

        (await ThreadAsync(client, "Task", aTask)).EnumerateArray().Single()
            .GetProperty("text").GetString().Should().Be("De la primera");
    }
}
