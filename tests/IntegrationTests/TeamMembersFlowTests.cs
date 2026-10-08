using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Editar quién está en un equipo.
///
/// <c>PUT /teams/{id}</c> recibía <c>memberIds</c> y lo ignoraba, y la API no decía quiénes eran
/// los miembros, sólo cuántos. La pantalla de administración abría la edición sin nadie marcado,
/// el administrador marcaba a quien quería, guardaba, y el equipo seguía igual.
///
/// Las pruebas leen el equipo después de guardar: lo que cuenta es quién <b>quedó</b> dentro.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TeamMembersFlowTests(CrmApiFactory factory)
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

    private static async Task<Guid> CreateTeamAsync(HttpClient client, params Guid[] memberIds)
    {
        var response = await client.PostAsJsonAsync("/api/v1/teams", new
        {
            name = "Equipo " + Guid.NewGuid().ToString("N")[..8],
            description = "Creado por las pruebas de miembros",
            memberIds
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<(int Count, List<Guid> Ids)> MembersAsync(HttpClient client, Guid teamId)
    {
        var team = await client.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{teamId}");
        var ids = team.GetProperty("memberIds").EnumerateArray().Select(e => e.GetGuid()).ToList();
        return (team.GetProperty("memberCount").GetInt32(), ids);
    }

    private static async Task UpdateAsync(HttpClient client, Guid teamId, object body)
    {
        var response = await client.PutAsJsonAsync($"/api/v1/teams/{teamId}", body);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_team_says_who_its_members_are()
    {
        var client = await AuthenticateAsync();
        Guid ana = Guid.NewGuid(), luis = Guid.NewGuid();
        var teamId = await CreateTeamAsync(client, ana, luis);

        var (count, ids) = await MembersAsync(client, teamId);
        count.Should().Be(2);
        ids.Should().BeEquivalentTo([ana, luis],
            "sin la lista, la pantalla de edición no puede marcar a nadie");
    }

    [Fact]
    public async Task Saving_the_list_adds_and_removes_members()
    {
        var client = await AuthenticateAsync();
        Guid ana = Guid.NewGuid(), luis = Guid.NewGuid(), marta = Guid.NewGuid();
        var teamId = await CreateTeamAsync(client, ana, luis);

        await UpdateAsync(client, teamId, new { name = "Equipo editado", description = "", memberIds = new[] { luis, marta } });

        var (count, ids) = await MembersAsync(client, teamId);
        ids.Should().BeEquivalentTo([luis, marta], "Ana sale, Marta entra y Luis se queda");
        count.Should().Be(2, "quien sale no se sigue contando");
    }

    [Fact]
    public async Task Someone_who_left_can_come_back()
    {
        var client = await AuthenticateAsync();
        Guid ana = Guid.NewGuid();
        var teamId = await CreateTeamAsync(client, ana);

        await UpdateAsync(client, teamId, new { name = "Equipo", description = "", memberIds = Array.Empty<Guid>() });
        (await MembersAsync(client, teamId)).Ids.Should().BeEmpty("una lista vacía deja el equipo sin nadie");

        await UpdateAsync(client, teamId, new { name = "Equipo", description = "", memberIds = new[] { ana } });
        (await MembersAsync(client, teamId)).Ids.Should().Equal(ana);
    }

    /// <summary>Quien sólo cambia el nombre y no manda la lista no vacía el equipo.</summary>
    [Fact]
    public async Task Without_a_list_the_members_stay()
    {
        var client = await AuthenticateAsync();
        Guid ana = Guid.NewGuid(), luis = Guid.NewGuid();
        var teamId = await CreateTeamAsync(client, ana, luis);

        await UpdateAsync(client, teamId, new { name = "Sólo cambia el nombre", description = "" });

        (await MembersAsync(client, teamId)).Ids.Should().BeEquivalentTo([ana, luis]);
    }
}
