using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Las menciones en comentarios: escribirlas con el formato que manda la pantalla y que desde lo
/// mencionado se vea quién habla de ello.
///
/// La prueba que importa es la que escribe por la API y pregunta desde el otro lado: comprueba la
/// unión entre lo que manda el componente y lo que lee el servidor, que es donde estas cosas se
/// rompen sin dar error. También cubre editar: al reescribir el comentario, sus menciones se
/// reescriben, y EF no puede tomar las nuevas por filas existentes.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CommentMentionsFlowTests(CrmApiFactory factory)
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

    private static async Task<Guid> CreateTaskAsync(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync("/api/v1/tasks", new
        {
            projectId = await TestProjects.CreateAsync(client),
            title,
            description = "Creada por las pruebas de menciones en comentarios",
            assigneeId = Guid.NewGuid(),
            estimatedHours = 1m,
            dueDate = "2026-12-01",
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<List<JsonElement>> MentioningAsync(HttpClient client, string type, Guid id)
        => (await client.GetFromJsonAsync<JsonElement>($"/api/v1/comments/mentions/{type}/{id}"))
            .EnumerateArray().ToList();

    [Fact]
    public async Task A_task_mentioned_in_a_comment_knows_which_comment_talks_about_it()
    {
        var client = await AuthenticateAsync();
        var commented = await CreateTaskAsync(client, "Tarea donde se comenta");
        var mentioned = await CreateTaskAsync(client, "Tarea mencionada");
        var team = Guid.NewGuid();

        var text = $"Depende de @[Tarea mencionada](Task:{mentioned}), que lleva @[Soporte](Team:{team})";
        var post = await client.PostAsJsonAsync($"/api/v1/comments/Task/{commented}", new { text });
        post.StatusCode.Should().Be(HttpStatusCode.Created, await post.Content.ReadAsStringAsync());

        var fromTask = await MentioningAsync(client, "Task", mentioned);
        fromTask.Should().ContainSingle();
        fromTask[0].GetProperty("entityType").GetString().Should().Be("Task");
        fromTask[0].GetProperty("entityId").GetGuid().Should().Be(commented, "dice dónde está el comentario");
        fromTask[0].GetProperty("excerpt").GetString().Should().Be("Depende de #Tarea mencionada, que lleva @Soporte",
            "el extracto se lee con nombres, no con identificadores");

        (await MentioningAsync(client, "Team", team)).Should().ContainSingle();
    }

    [Fact]
    public async Task Editing_rewrites_the_mentions()
    {
        var client = await AuthenticateAsync();
        var commented = await CreateTaskAsync(client, "Tarea con un comentario que cambia");
        Guid first = Guid.NewGuid(), second = Guid.NewGuid();

        var post = await client.PostAsJsonAsync($"/api/v1/comments/Task/{commented}",
            new { text = $"Para @[Ana](Person:{first})" });
        var commentId = (await post.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var edit = await client.PutAsJsonAsync($"/api/v1/comments/{commentId}",
            new { text = $"Mejor para @[Luis](Person:{second})" });
        edit.IsSuccessStatusCode.Should().BeTrue(await edit.Content.ReadAsStringAsync());

        (await MentioningAsync(client, "Person", first)).Should().BeEmpty("Ana ya no está en el texto");
        (await MentioningAsync(client, "Person", second)).Should().ContainSingle();
    }

    [Fact]
    public async Task A_deleted_comment_no_longer_mentions_anything()
    {
        var client = await AuthenticateAsync();
        var commented = await CreateTaskAsync(client, "Tarea con un comentario que se borra");
        var person = Guid.NewGuid();

        var post = await client.PostAsJsonAsync($"/api/v1/comments/Task/{commented}",
            new { text = $"Para @[Ana](Person:{person})" });
        var commentId = (await post.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await client.DeleteAsync($"/api/v1/comments/{commentId}")).EnsureSuccessStatusCode();

        (await MentioningAsync(client, "Person", person)).Should().BeEmpty();
    }

    [Fact]
    public async Task An_unknown_type_is_rejected()
    {
        var client = await AuthenticateAsync();

        var response = await client.GetAsync($"/api/v1/comments/mentions/Planet/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
