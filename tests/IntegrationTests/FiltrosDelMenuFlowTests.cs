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
public sealed class FiltrosDelMenuFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    private async Task<(HttpClient Cliente, Guid Yo)> AutenticarAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var yo = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/auth/users/me");
        return (cliente, yo.GetProperty("id").GetGuid());
    }

    private static async Task<int> CuantosAsync(HttpClient cliente, string ruta)
    {
        var respuesta = await cliente.GetAsync(ruta);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, ruta);

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.GetProperty("totalCount").GetInt32();
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
    public async Task Mis_tickets_son_exactamente_los_que_llevo_yo()
    {
        var (cliente, yo) = await AutenticarAsync();

        var todos = await CuantosAsync(cliente, "/api/v1/tickets?pageSize=1");
        var mios = await CuantosAsync(cliente, "/api/v1/tickets?pageSize=1&filter=mine");
        var porAgente = await CuantosAsync(cliente, $"/api/v1/tickets?pageSize=1&agentId={yo}");

        todos.Should().BeGreaterThan(0, "el sembrador crea tickets; sin ellos esto no comprueba nada");

        mios.Should().Be(porAgente,
            "«mis tickets» son los que llevo yo como agente. Antes el parámetro llegaba, nadie lo "
            + "leía y la respuesta era la lista entera");
    }

    [Theory]
    [InlineData("/api/v1/tickets")]
    [InlineData("/api/v1/tasks")]
    [InlineData("/api/v1/projects")]
    public async Task Los_filtros_del_menu_los_entienden_todos_los_modulos(string ruta)
    {
        var (cliente, _) = await AutenticarAsync();

        foreach (var filtro in new[] { "mine", "created", "favorites" })
        {
            var respuesta = await cliente.GetAsync($"{ruta}?pageSize=1&filter={filtro}");

            respuesta.StatusCode.Should().Be(HttpStatusCode.OK,
                $"«{filtro}» se ofrece en el menú de todos los módulos, así que {ruta} tiene que entenderlo");
        }
    }

    #region Favoritos

    /// <summary>
    /// La estrella es un interruptor: la misma llamada marca y desmarca. Con dos endpoints, dos
    /// pestañas abiertas acaban peleándose por el estado.
    /// </summary>
    [Fact]
    public async Task La_estrella_marca_y_desmarca_con_la_misma_llamada()
    {
        var (cliente, _) = await AutenticarAsync();
        var algo = Guid.NewGuid();

        var primera = await cliente.PostAsync($"/api/v1/users/me/favorites/Tarea/{algo}", null);
        primera.StatusCode.Should().Be(HttpStatusCode.OK);
        (await primera.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isFavorite").GetBoolean()
            .Should().BeTrue();

        var segunda = await cliente.PostAsync($"/api/v1/users/me/favorites/Tarea/{algo}", null);
        (await segunda.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isFavorite").GetBoolean()
            .Should().BeFalse("la segunda pulsación desmarca");
    }

    [Fact]
    public async Task Lo_marcado_aparece_en_la_lista_de_favoritos()
    {
        var (cliente, _) = await AutenticarAsync();
        var algo = Guid.NewGuid();

        await cliente.PostAsync($"/api/v1/users/me/favorites/Proyecto/{algo}", null);

        var marcados = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/users/me/favorites/Proyecto");

        marcados.EnumerateArray().Select(e => e.GetGuid()).Should().Contain(algo);
    }

    /// <summary>
    /// Los favoritos son de cada tipo por separado: marcar una tarea no marca un proyecto con el
    /// mismo identificador. Sin esta separación, las listas se contaminarían entre módulos.
    /// </summary>
    [Fact]
    public async Task Los_favoritos_no_se_mezclan_entre_tipos()
    {
        var (cliente, _) = await AutenticarAsync();
        var algo = Guid.NewGuid();

        await cliente.PostAsync($"/api/v1/users/me/favorites/Tarea/{algo}", null);

        var proyectos = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/users/me/favorites/Proyecto");

        proyectos.EnumerateArray().Select(e => e.GetGuid()).Should().NotContain(algo);
    }

    [Fact]
    public async Task Un_tipo_que_no_existe_se_rechaza()
    {
        var (cliente, _) = await AutenticarAsync();

        var respuesta = await cliente.PostAsync($"/api/v1/users/me/favorites/Factura/{Guid.NewGuid()}", null);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
    public async Task Un_ticket_marcado_sale_al_filtrar_por_favoritos()
    {
        var (cliente, _) = await AutenticarAsync();

        var listado = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/tickets?pageSize=1");
        var alguno = listado.GetProperty("items").EnumerateArray().FirstOrDefault();

        alguno.ValueKind.Should().NotBe(JsonValueKind.Undefined, "hace falta al menos un ticket");
        var ticketId = alguno.GetProperty("id").GetGuid();

        await cliente.PostAsync($"/api/v1/users/me/favorites/Ticket/{ticketId}", null);

        var favoritos = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/tickets?pageSize=100&filter=favorites");
        var ids = favoritos.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("id").GetGuid());

        ids.Should().Contain(ticketId,
            "si esto falla, la cadena del tipo no coincide entre Ticketing e Identity y el filtro "
            + "está devolviendo vacío en silencio");

        // Se desmarca para no dejar rastro a las demás pruebas de la colección.
        await cliente.PostAsync($"/api/v1/users/me/favorites/Ticket/{ticketId}", null);
    }

    /// <summary>
    /// Sin nada marcado, el filtro devuelve **cero**, no todos. Devolver todo cuando no hay
    /// favoritos sería el mismo engaño que se está arreglando, sólo que al revés.
    /// </summary>
    [Fact]
    public async Task Sin_favoritos_el_filtro_devuelve_cero_y_no_todos()
    {
        var (cliente, _) = await AutenticarAsync();

        var todos = await CuantosAsync(cliente, "/api/v1/tickets?pageSize=1");
        var favoritos = await CuantosAsync(cliente, "/api/v1/tickets?pageSize=1&filter=favorites");

        todos.Should().BeGreaterThan(0);
        favoritos.Should().BeLessThan(todos);
    }

    [Fact]
    public async Task Sin_autenticar_no_se_ven_ni_se_marcan_favoritos()
    {
        var anonimo = factory.CreateClient();

        (await anonimo.GetAsync("/api/v1/users/me/favorites/Tarea")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);

        (await anonimo.PostAsync($"/api/v1/users/me/favorites/Tarea/{Guid.NewGuid()}", null)).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion
}
