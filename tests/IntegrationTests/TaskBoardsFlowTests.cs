using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Los tableros de tareas por ámbito: de un proyecto, de un equipo, de mis equipos.
///
/// <list type="bullet">
/// <item><b>Toda tarea pertenece a un proyecto que existe.</b> El identificador se guardaba sin
/// mirarlo, así que una tarea podía colgar de un proyecto inventado y no salir en el tablero de
/// ninguno.</item>
/// <item><b>El tablero de un equipo</b> no existía: no había forma de pedir las tareas de sus
/// miembros.</item>
/// <item><b>«De mi equipo»</b> buscaba el id de la persona dentro de las etiquetas de la tarea, y
/// no devolvía nunca nada.</item>
/// </list>
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TaskBoardsFlowTests(CrmApiFactory factory)
{
    private async Task<(HttpClient Client, Guid Me)> AuthenticateAsync()
    {
        var login = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { Email = "admin@acme.com", Password = "admin123" });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/users/me");
        return (client, me.GetProperty("id").GetGuid());
    }

    private static Task<HttpResponseMessage> PostTaskAsync(HttpClient client, Guid projectId, string title, Guid assignee)
        => client.PostAsJsonAsync("/api/v1/tasks", new
        {
            projectId,
            title,
            description = "Creada por las pruebas de tableros",
            assigneeId = assignee,
            estimatedHours = 1m,
            dueDate = "2026-12-01",
        });

    private static async Task<Guid> CreateTaskAsync(HttpClient client, Guid projectId, string title, Guid assignee)
    {
        var response = await PostTaskAsync(client, projectId, title, assignee);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateTeamAsync(HttpClient client, params Guid[] memberIds)
    {
        var response = await client.PostAsJsonAsync("/api/v1/teams", new
        {
            name = "Equipo de tablero " + Guid.NewGuid().ToString("N")[..8],
            description = "Creado por las pruebas de tableros",
            memberIds
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<List<Guid>> TaskIdsAsync(HttpClient client, string query)
    {
        var page = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks?pageSize=1000&{query}");
        return page.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("id").GetGuid()).ToList();
    }

    [Fact]
    public async Task A_task_in_a_project_that_does_not_exist_is_rejected()
    {
        var (client, me) = await AuthenticateAsync();

        var response = await PostTaskAsync(client, Guid.NewGuid(), "Tarea sin proyecto de verdad", me);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "una tarea con un proyecto inventado no sale en el tablero de ningún proyecto");
    }

    [Fact]
    public async Task The_project_board_shows_its_tasks_and_no_others()
    {
        var (client, me) = await AuthenticateAsync();
        var project = await TestProjects.CreateAsync(client);
        var other = await TestProjects.CreateAsync(client);

        var mine = await CreateTaskAsync(client, project, "En el proyecto", me);
        var elsewhere = await CreateTaskAsync(client, other, "En otro proyecto", me);

        var ids = await TaskIdsAsync(client, $"projectId={project}");
        ids.Should().Contain(mine).And.NotContain(elsewhere);
    }

    [Fact]
    public async Task The_team_board_shows_what_its_members_carry()
    {
        var (client, _) = await AuthenticateAsync();
        var project = await TestProjects.CreateAsync(client);
        Guid ana = Guid.NewGuid(), outsider = Guid.NewGuid();
        var team = await CreateTeamAsync(client, ana);

        var anas = await CreateTaskAsync(client, project, "La lleva Ana", ana);
        var notTheTeams = await CreateTaskAsync(client, project, "La lleva alguien de fuera", outsider);

        var ids = await TaskIdsAsync(client, $"teamId={team}");
        ids.Should().Contain(anas).And.NotContain(notTheTeams);
    }

    [Fact]
    public async Task A_team_that_does_not_exist_is_a_404_not_an_empty_board()
    {
        var (client, _) = await AuthenticateAsync();

        var response = await client.GetAsync($"/api/v1/tasks?teamId={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task My_team_filter_shows_what_my_teammates_carry()
    {
        var (client, me) = await AuthenticateAsync();
        var project = await TestProjects.CreateAsync(client);
        Guid mate = Guid.NewGuid(), stranger = Guid.NewGuid();
        await CreateTeamAsync(client, me, mate);

        var mates = await CreateTaskAsync(client, project, "La lleva mi compañera", mate);
        var strangers = await CreateTaskAsync(client, project, "La lleva alguien sin equipo conmigo", stranger);

        var ids = await TaskIdsAsync(client, "filter=team");
        ids.Should().Contain(mates, "«de mi equipo» es lo que lleva gente de mis equipos")
            .And.NotContain(strangers);
    }
}
