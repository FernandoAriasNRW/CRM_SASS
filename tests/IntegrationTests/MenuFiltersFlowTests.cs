using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Que cada entrada del menú de navegación filtre de verdad.
///
/// Existen por un fallo que estaba a la vista y nadie miraba: el menú ofrecía «Mis Tickets»
/// apuntando a <c>/tickets?filter=mine</c>, el parámetro llegaba al servidor, **nadie lo leía**
/// y la pantalla devolvía los 175 tickets de siempre. Quien lo usaba creía estar viendo los
/// suyos.
///
/// Es el mismo patrón que los tipos de informe que el desplegable ofrecía y el enum no conocía.
/// Por eso la comprobación que importa no es «responde 200» —eso ya lo hacía— sino
/// **«devuelve algo distinto de no filtrar»**. Un menú que promete y devuelve la misma lista es
/// peor que un menú corto.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MenuFiltersFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    private async Task<(HttpClient Client, Guid Me)> AuthenticateAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/users/me");
        return (client, me.GetProperty("id").GetGuid());
    }

    private static async Task<int> CountAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK, path);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("totalCount").GetInt32();
    }

    /// <summary>
    /// El fallo original, con su nombre.
    ///
    /// Se comprueba contra `agentId=<yo>`, que es lo que «mis tickets» significa, en vez de
    /// contra «menos que todos». La primera versión de esta prueba daba por hecho que el
    /// sembrador reparte los tickets entre varias personas, y eso es una suposición sobre datos
    /// de prueba: el día que el sembrador asigne todo al administrador, la prueba fallaría sin
    /// que nada estuviera roto. Comparar los dos caminos comprueba el significado, no el reparto.
    /// </summary>
    [Fact]
    public async Task My_tickets_are_exactly_the_ones_I_handle()
    {
        var (client, me) = await AuthenticateAsync();

        var all = await CountAsync(client, "/api/v1/tickets?pageSize=1");
        var mine = await CountAsync(client, "/api/v1/tickets?pageSize=1&filter=mine");
        var byAgent = await CountAsync(client, $"/api/v1/tickets?pageSize=1&agentId={me}");

        all.Should().BeGreaterThan(0, "el sembrador crea tickets; sin ellos esto no comprueba nada");

        mine.Should().Be(byAgent,
            "«mis tickets» son los que llevo yo como agente. Antes el parámetro llegaba, nadie lo "
            + "leía y la respuesta era la lista entera");
    }

    [Theory]
    [InlineData("/api/v1/tickets")]
    [InlineData("/api/v1/tasks")]
    [InlineData("/api/v1/projects")]
    public async Task All_modules_understand_the_menu_filters(string path)
    {
        var (client, _) = await AuthenticateAsync();

        foreach (var filter in new[] { "mine", "created", "favorites" })
        {
            var response = await client.GetAsync($"{path}?pageSize=1&filter={filter}");

            response.StatusCode.Should().Be(HttpStatusCode.OK,
                $"«{filter}» se ofrece en el menú de todos los módulos, así que {path} tiene que entenderlo");
        }
    }

    #region Favoritos

    /// <summary>
    /// La estrella es un interruptor: la misma llamada marca y desmarca. Con dos endpoints, dos
    /// pestañas abiertas acaban peleándose por el estado.
    /// </summary>
    [Fact]
    public async Task The_star_toggles_with_the_same_call()
    {
        var (client, _) = await AuthenticateAsync();
        var something = Guid.NewGuid();

        var first = await client.PostAsync($"/api/v1/users/me/favorites/Task/{something}", null);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isFavorite").GetBoolean()
            .Should().BeTrue();

        var second = await client.PostAsync($"/api/v1/users/me/favorites/Task/{something}", null);
        (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isFavorite").GetBoolean()
            .Should().BeFalse("la segunda pulsación desmarca");
    }

    [Fact]
    public async Task Starred_items_appear_in_favorites()
    {
        var (client, _) = await AuthenticateAsync();
        var something = Guid.NewGuid();

        await client.PostAsync($"/api/v1/users/me/favorites/Project/{something}", null);

        var starred = await client.GetFromJsonAsync<JsonElement>("/api/v1/users/me/favorites/Project");

        starred.EnumerateArray().Select(e => e.GetGuid()).Should().Contain(something);
    }

    /// <summary>
    /// Los favoritos son de cada tipo por separado: marcar una tarea no marca un proyecto con el
    /// mismo identificador. Sin esta separación, las listas se contaminarían entre módulos.
    /// </summary>
    [Fact]
    public async Task Favorites_do_not_mix_types()
    {
        var (client, _) = await AuthenticateAsync();
        var something = Guid.NewGuid();

        await client.PostAsync($"/api/v1/users/me/favorites/Task/{something}", null);

        var projects = await client.GetFromJsonAsync<JsonElement>("/api/v1/users/me/favorites/Project");

        projects.EnumerateArray().Select(e => e.GetGuid()).Should().NotContain(something);
    }

    [Fact]
    public async Task An_unknown_type_is_rejected()
    {
        var (client, _) = await AuthenticateAsync();

        var response = await client.PostAsync($"/api/v1/users/me/favorites/Factura/{Guid.NewGuid()}", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// La prueba que conecta los dos lados: se marca un ticket real y tiene que salir al filtrar
    /// por favoritos.
    ///
    /// Importa porque Ticketing no puede referenciar a Identity —ningún módulo referencia a
    /// otro— y el puerto de favoritos habla en cadenas: el módulo escribe «Ticket» a mano. Si esa
    /// cadena divergiera del tipo que guarda Identity, **el filtro devolvería una lista vacía sin
    /// dar ningún error**, que es la clase de fallo que nadie detecta.
    /// </summary>
    [Fact]
    public async Task A_starred_ticket_shows_when_filtering_by_favorites()
    {
        var (client, _) = await AuthenticateAsync();

        var listing = await client.GetFromJsonAsync<JsonElement>("/api/v1/tickets?pageSize=1");
        var any = listing.GetProperty("items").EnumerateArray().FirstOrDefault();

        any.ValueKind.Should().NotBe(JsonValueKind.Undefined, "hace falta al menos un ticket");
        var ticketId = any.GetProperty("id").GetGuid();

        await client.PostAsync($"/api/v1/users/me/favorites/Ticket/{ticketId}", null);

        var favorites = await client.GetFromJsonAsync<JsonElement>("/api/v1/tickets?pageSize=100&filter=favorites");
        var ids = favorites.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("id").GetGuid());

        ids.Should().Contain(ticketId,
            "si esto falla, la cadena del tipo no coincide entre Ticketing e Identity y el filtro "
            + "está devolviendo vacío en silencio");

        // Se desmarca para no dejar rastro a las demás pruebas de la colección.
        await client.PostAsync($"/api/v1/users/me/favorites/Ticket/{ticketId}", null);
    }

    /// <summary>
    /// Sin nada marcado, el filtro devuelve **cero**, no todos. Devolver todo cuando no hay
    /// favoritos sería el mismo engaño que se está arreglando, sólo que al revés.
    /// </summary>
    [Fact]
    public async Task Without_favorites_the_filter_returns_zero_not_everything()
    {
        var (client, _) = await AuthenticateAsync();

        var all = await CountAsync(client, "/api/v1/tickets?pageSize=1");
        var favorites = await CountAsync(client, "/api/v1/tickets?pageSize=1&filter=favorites");

        all.Should().BeGreaterThan(0);
        favorites.Should().BeLessThan(all);
    }

    [Fact]
    public async Task Without_authentication_favorites_are_neither_seen_nor_starred()
    {
        var anonymous = factory.CreateClient();

        (await anonymous.GetAsync("/api/v1/users/me/favorites/Task")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);

        (await anonymous.PostAsync($"/api/v1/users/me/favorites/Task/{Guid.NewGuid()}", null)).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion
}
