using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Que la API mande y enlace los campos con los nombres que lee y escribe el frontend.
///
/// El frontend no está tipado contra el backend, y sus pruebas no lo pueden ver: las e2e simulan
/// la API con <c>page.route</c> y Karma usa espías, así que una y otra se escriben con los nombres
/// que el frontend espera, no con los que la API manda. Un campo con otro nombre no da ningún
/// error: llega <c>undefined</c> y la pantalla se queda vacía o en cero.
///
/// Cada caso de aquí es uno que estaba roto así, y lee el JSON real con el nombre exacto que usa
/// el componente (se cita en cada prueba). Si alguien renombra el campo en un lado, esto falla.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class FrontendContractTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    private async Task<HttpClient> AuthenticateAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    /// <summary>
    /// <c>app.config.ts</c> renueva la sesión al arrancar y leía <c>expiresAt</c>: la caducidad
    /// quedaba en una fecha inválida.
    /// </summary>
    [Fact]
    public async Task Refresh_returns_the_expiry_under_the_name_the_app_reads()
    {
        // El cliente guarda la cookie de renovación que pone el login.
        var client = factory.CreateClient();
        (await client.PostAsJsonAsync("/api/v1/auth/login", new { Email, Password })).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("accessTokenExpiresAtUtc").GetDateTime().Should().BeAfter(DateTime.UtcNow);
    }

    /// <summary>
    /// <c>chat.component.ts</c>: el mensaje va en el cuerpo (<c>{ content }</c>), y se leen
    /// <c>id</c>, <c>conversationId</c>, <c>sentAt</c> y el <c>type</c> del canal. El texto se
    /// esperaba en la consulta, el identificador salía vacío y el canal no traía su tipo.
    /// </summary>
    [Fact]
    public async Task Chat_takes_the_message_in_the_body_and_returns_what_the_screen_reads()
    {
        var client = await AuthenticateAsync();

        var created = await client.PostAsJsonAsync("/api/v1/channels", new { name = "#contrato", type = "Channel" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var channelId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var sent = await client.PostAsJsonAsync($"/api/v1/channels/{channelId}/messages", new { content = "Hola, equipo" });
        sent.StatusCode.Should().Be(HttpStatusCode.Created);

        var channels = await client.GetFromJsonAsync<JsonElement>("/api/v1/channels?pageSize=100");
        channels.GetProperty("items").EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == channelId)
            .GetProperty("type").GetString().Should().Be("Channel");

        var messages = await client.GetFromJsonAsync<JsonElement>($"/api/v1/channels/{channelId}/messages");
        var message = messages.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        message.GetProperty("id").GetGuid().Should().NotBe(Guid.Empty);
        message.GetProperty("conversationId").GetGuid().Should().Be(channelId);
        message.GetProperty("content").GetString().Should().Be("Hola, equipo");
        message.GetProperty("sentAt").GetDateTime().Should().NotBe(default);
    }

    /// <summary>
    /// <c>admin-teams.component.ts</c> leía <c>members</c>, que no llega: todos los equipos salían
    /// con 0 miembros. Lo que manda la API es <c>memberCount</c>.
    /// </summary>
    [Fact]
    public async Task Teams_report_how_many_members_they_have()
    {
        var client = await AuthenticateAsync();
        var me = (await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/users/me")).GetProperty("id").GetGuid();

        var created = await client.PostAsJsonAsync("/api/v1/teams", new
        {
            name = "Equipo del contrato",
            description = "creado por las pruebas de integración",
            memberIds = new[] { me }
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var teamId = await created.Content.ReadFromJsonAsync<Guid>();

        var teams = await client.GetFromJsonAsync<JsonElement>("/api/v1/teams");
        teams.EnumerateArray()
            .Single(t => t.GetProperty("id").GetGuid() == teamId)
            .GetProperty("memberCount").GetInt32().Should().Be(1);
    }

    /// <summary>
    /// <c>list-query.ts</c> manda <c>page</c> y <c>search</c>. Las listas mandaban
    /// <c>pageNumber</c> y <c>searchTerm</c>, que la API ignora: la segunda página era la primera
    /// y el buscador no filtraba.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/tasks")]
    [InlineData("/api/v1/tickets")]
    [InlineData("/api/v1/projects")]
    public async Task Lists_page_and_search_with_the_names_the_tables_send(string path)
    {
        var client = await AuthenticateAsync();

        var all = await client.GetFromJsonAsync<JsonElement>($"{path}?page=1&pageSize=1000");
        all.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(1, "hacen falta dos filas para ver una segunda página");

        var first = await client.GetFromJsonAsync<JsonElement>($"{path}?page=1&pageSize=1");
        var second = await client.GetFromJsonAsync<JsonElement>($"{path}?page=2&pageSize=1");
        Id(second).Should().NotBe(Id(first));

        var nothing = await client.GetFromJsonAsync<JsonElement>($"{path}?search=zzz-no-existe-zzz");
        nothing.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    private static Guid Id(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetGuid();
}
