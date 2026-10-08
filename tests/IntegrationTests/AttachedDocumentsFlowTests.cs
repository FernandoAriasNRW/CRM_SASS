using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Documentos adjuntos a una tarea, y la vuelta: en qué tareas está un documento.
///
/// Los documentos viven en Docs y la tarea sólo guarda cuáles tiene, así que lo que se prueba es la
/// unión: adjuntar por la API de tareas y que salga con el título que tiene en Docs, y que desde el
/// documento se vea la tarea. Es donde estas cosas se rompen sin dar error.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AttachedDocumentsFlowTests(CrmApiFactory factory)
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

    private static async Task<(Guid Id, string Title)> CreateDocumentAsync(HttpClient client)
    {
        var title = $"Especificación {Guid.NewGuid():N}"[..28];
        var creation = await client.PostAsJsonAsync("/api/v1/docs", new
        {
            Title = title,
            Description = "Para adjuntar a una tarea",
            Type = 0,
            TeamId = (Guid?)null,
            ProjectId = (Guid?)null,
            InitialContent = (string?)null
        });
        creation.EnsureSuccessStatusCode();

        // El endpoint devuelve el identificador como una cadena suelta.
        return ((await creation.Content.ReadFromJsonAsync<JsonElement>()).GetGuid(), title);
    }

    private static async Task<Guid> CreateTaskAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/tasks", new
        {
            projectId = await TestProjects.CreateAsync(client),
            title = "Tarea con documentos",
            description = "Creada por las pruebas de adjuntos",
            assigneeId = Guid.NewGuid(),
            estimatedHours = 1m,
            dueDate = "2026-12-01",
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<List<JsonElement>> AttachedAsync(HttpClient client, Guid taskId)
        => (await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{taskId}/documents"))
            .EnumerateArray().ToList();

    [Fact]
    public async Task An_attached_document_shows_on_the_task_with_its_title()
    {
        var client = await AuthenticateAsync();
        var (documentId, title) = await CreateDocumentAsync(client);
        var taskId = await CreateTaskAsync(client);

        var attach = await client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/documents", new { documentId });
        attach.StatusCode.Should().Be(HttpStatusCode.OK, await attach.Content.ReadAsStringAsync());

        var attached = await AttachedAsync(client, taskId);
        attached.Should().ContainSingle();
        attached[0].GetProperty("documentId").GetGuid().Should().Be(documentId);
        attached[0].GetProperty("title").GetString().Should().Be(title,
            "el título se pregunta a Docs: la tarea sólo guarda qué documento es");
    }

    [Fact]
    public async Task Attaching_twice_leaves_one_and_detaching_removes_it()
    {
        var client = await AuthenticateAsync();
        var (documentId, _) = await CreateDocumentAsync(client);
        var taskId = await CreateTaskAsync(client);

        (await client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/documents", new { documentId })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/documents", new { documentId })).EnsureSuccessStatusCode();
        (await AttachedAsync(client, taskId)).Should().ContainSingle("la tarea tiene ese documento o no lo tiene");

        var detach = await client.DeleteAsync($"/api/v1/tasks/{taskId}/documents/{documentId}");
        detach.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AttachedAsync(client, taskId)).Should().BeEmpty();
    }

    [Fact]
    public async Task The_document_knows_which_tasks_it_is_attached_to()
    {
        var client = await AuthenticateAsync();
        var (documentId, _) = await CreateDocumentAsync(client);
        var taskId = await CreateTaskAsync(client);

        (await client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/documents", new { documentId })).EnsureSuccessStatusCode();

        var tasks = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/with-document/{documentId}");
        tasks.EnumerateArray().Select(t => t.GetProperty("taskId").GetGuid()).Should().Contain(taskId);
        tasks.EnumerateArray().First().GetProperty("title").GetString().Should().Be("Tarea con documentos");
    }

    [Fact]
    public async Task A_document_that_does_not_exist_cannot_be_attached()
    {
        var client = await AuthenticateAsync();
        var taskId = await CreateTaskAsync(client);

        var attach = await client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/documents", new { documentId = Guid.NewGuid() });

        attach.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await AttachedAsync(client, taskId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Attaching_to_a_task_that_does_not_exist_is_a_404()
    {
        var client = await AuthenticateAsync();
        var (documentId, _) = await CreateDocumentAsync(client);

        var attach = await client.PostAsJsonAsync($"/api/v1/tasks/{Guid.NewGuid()}/documents", new { documentId });

        attach.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_deleted_document_no_longer_shows_on_the_task()
    {
        var client = await AuthenticateAsync();
        var (documentId, _) = await CreateDocumentAsync(client);
        var taskId = await CreateTaskAsync(client);
        (await client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/documents", new { documentId })).EnsureSuccessStatusCode();

        (await client.DeleteAsync($"/api/v1/docs/{documentId}")).EnsureSuccessStatusCode();

        (await AttachedAsync(client, taskId)).Should().BeEmpty("un documento borrado no tiene nada que abrir");
    }
}
