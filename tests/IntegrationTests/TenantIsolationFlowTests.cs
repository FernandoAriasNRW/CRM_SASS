using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Que nadie pueda escribir en la organización de otro.
///
/// Existen por un fallo que estaba en producción y que nadie había visto: los <c>POST</c> de
/// varios módulos enlazaban el comando directamente del cuerpo de la petición, y esos comandos
/// llevan <c>TenantId</c> dentro. Bastaba con poner un Guid cualquiera en el JSON para crear
/// datos dentro del inquilino de otra empresa. Se comprobó mandando un tenant ajeno: el
/// servidor respondía 201 y guardaba con ese tenant.
///
/// Tenía además un segundo efecto, más visible y menos grave: quien no mandaba el campo creaba
/// la entidad con <c>Guid.Empty</c>, así que el filtro global no volvía a verla nunca. El alta
/// devolvía 201 y la entidad era invisible en el listado inmediatamente después.
///
/// Estas pruebas mandan a propósito un tenant que no es el suyo. La forma de comprobarlo es
/// mirar **con qué tenant se guardó**, no el código de estado: aceptar la petición e ignorar el
/// campo es una respuesta correcta; guardarla con el tenant ajeno, no.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TenantIsolationFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    private async Task<(HttpClient Client, Guid Tenant, Guid User)> AuthenticateAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/users/me");
        return (client, me.GetProperty("tenantId").GetGuid(), me.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task A_project_is_saved_in_my_tenant_even_if_the_body_says_otherwise()
    {
        var (client, myTenant, me) = await AuthenticateAsync();

        var response = await client.PostAsJsonAsync("/api/v1/projects", new
        {
            tenantId = Guid.NewGuid(),   // no es el mío
            ownerId = Guid.NewGuid(),    // tampoco soy yo
            spaceId = Guid.NewGuid(),
            name = "Proyecto con inquilino ajeno en el cuerpo",
            description = "Debe acabar en mi inquilino, no en el que pide el JSON",
            estimatedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        created.GetProperty("tenantId").GetGuid().Should().Be(myTenant,
            "el inquilino sale del token; si sale del cuerpo, cualquiera escribe en los datos de otra empresa");
        created.GetProperty("ownerId").GetGuid().Should().Be(me,
            "el dueño es quien lo crea, no quien diga la petición");
    }

    /// <summary>
    /// La otra cara del mismo fallo: si el alta guarda con un inquilino que no es el mío, el
    /// listado —que sí filtra— no lo encuentra. Un 201 seguido de una lista vacía.
    /// </summary>
    [Fact]
    public async Task A_newly_created_project_appears_in_the_listing()
    {
        var (client, _, _) = await AuthenticateAsync();
        var name = "Proyecto visible " + Guid.NewGuid();

        var creation = await client.PostAsJsonAsync("/api/v1/projects", new
        {
            spaceId = Guid.NewGuid(),
            name = name,
            description = "Tiene que salir en el listado justo después de crearlo",
            estimatedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
        });
        creation.StatusCode.Should().Be(HttpStatusCode.Created);

        var listing = await client.GetFromJsonAsync<JsonElement>("/api/v1/projects?pageSize=100");
        var names = listing.GetProperty("items").EnumerateArray()
            .Select(p => p.GetProperty("name").ValueKind == JsonValueKind.Object
                ? p.GetProperty("name").GetProperty("value").GetString()
                : p.GetProperty("name").GetString())
            .ToList();

        names.Should().Contain(name,
            "un alta que responde 201 y luego no se ve es peor que un error: nadie sabe que se perdió");
    }

    [Fact]
    public async Task A_task_is_saved_in_my_tenant_even_if_the_body_says_otherwise()
    {
        var (client, myTenant, me) = await AuthenticateAsync();

        var response = await client.PostAsJsonAsync("/api/v1/tasks", new
        {
            tenantId = Guid.NewGuid(),
            createdById = Guid.NewGuid(),
            projectId = await TestProjects.CreateAsync(client),
            title = "Tarea con inquilino ajeno en el cuerpo",
            description = "Debe acabar en mi inquilino",
            assigneeId = Guid.Empty,
            estimatedHours = 1,
            dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        created.GetProperty("tenantId").GetGuid().Should().Be(myTenant);
        created.GetProperty("createdById").GetGuid().Should().Be(me);
    }

    /// <summary>
    /// Una suscripción de webhook plantada en otra organización recibiría sus eventos en una
    /// URL elegida por quien la plantó. De los que había, era el más caro.
    /// </summary>
    [Fact]
    public async Task A_webhook_is_saved_in_my_tenant_even_if_the_body_says_otherwise()
    {
        var (client, _, _) = await AuthenticateAsync();

        var response = await client.PostAsJsonAsync("/api/v1/webhooks", new
        {
            tenantId = Guid.NewGuid(),   // no es el mío
            name = "Webhook con inquilino ajeno en el cuerpo",
            url = "https://ejemplo.invalido/hook",
            eventTypes = new[] { "task.created" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("subscription").GetProperty("id").GetGuid();

        // La lista sólo enseña lo de mi organización: si se hubiera guardado con el inquilino del
        // cuerpo, no saldría aquí.
        var mine = await client.GetFromJsonAsync<JsonElement>("/api/v1/webhooks");
        mine.EnumerateArray().Select(w => w.GetProperty("id").GetGuid()).Should().Contain(id,
            "el inquilino sale del token; si saliera del cuerpo, el webhook recibiría los eventos de otra empresa");
    }

    /// <summary>
    /// El tablero puede mover una tarjeta.
    ///
    /// No podía. El tablero llama con <c>POST /tasks/{id}/move</c> y un cuerpo
    /// <c>{ newStatus }</c>, y la API sólo tenía un <c>PATCH</c> que leía <c>?status=</c>. Cada
    /// arrastre chocaba con un 405, la tarjeta volvía a su columna por el camino de revertir y
    /// salía un aviso de error. Arrastrar en el tablero no había funcionado nunca, y ninguna
    /// prueba lo cubría porque las de extremo a extremo simulan la respuesta de la API.
    /// </summary>
    [Fact]
    public async Task The_board_moves_a_task_with_the_real_call()
    {
        var (client, _, _) = await AuthenticateAsync();
        var task = await CreateTaskAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/tasks/{task}/move", new { newStatus = "In Progress" });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "es exactamente la petición que manda el tablero al soltar una tarjeta");

        var reloaded = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{task}");
        var status = reloaded.GetProperty("status");
        var value = status.ValueKind == JsonValueKind.Object
            ? status.GetProperty("value").GetString()
            : status.GetString();

        value.Should().Be("In Progress",
            "un 200 que no guarda es peor que un error: la tarjeta se queda donde la soltaron y al recargar vuelve");
    }

    /// <summary>
    /// Los permisos no se piden por la URL.
    ///
    /// El endpoint de mover recibía <c>actorId</c> y <c>actorRole</c> por la cadena de consulta,
    /// y el manejador autoriza con <c>if (ActorRole != "Admin" &amp;&amp; AssigneeId != ActorId)</c>.
    /// Añadir <c>&amp;actorRole=Admin</c> a la URL saltaba la comprobación entera.
    ///
    /// Se comprueba al revés, que es lo que se puede comprobar con un solo usuario: mandando un
    /// rol **peor** que el real. Si el servidor siguiera haciendo caso a la URL, este movimiento
    /// fallaría; como el rol sale del token, se ignora y la tarea se mueve.
    /// </summary>
    [Fact]
    public async Task The_role_in_the_url_decides_nothing()
    {
        var (client, _, _) = await AuthenticateAsync();
        var task = await CreateTaskAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/tasks/{task}/move?actorRole=Guest&actorId={Guid.NewGuid()}",
            new { newStatus = "In Progress" });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "quien manda es el token; lo que diga la URL sobre el rol es ruido y debe ignorarse");
    }

    private static async Task<Guid> CreateTaskAsync(HttpClient client)
    {
        var creation = await client.PostAsJsonAsync("/api/v1/tasks", new
        {
            projectId = await TestProjects.CreateAsync(client),
            title = "Tarea para mover",
            description = "Creada por la prueba",
            assigneeId = Guid.Empty,
            estimatedHours = 2,
            dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
        });
        creation.EnsureSuccessStatusCode();
        return (await creation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>
    /// El hash de la contraseña no sale de la API. Salía: <c>GET /auth/users/me</c> devolvía el
    /// DTO del usuario con el bcrypt dentro, así que viajaba al navegador en cada arranque y se
    /// quedaba por el camino en cachés y registros. Se comprueba sobre el texto crudo, no sobre
    /// un objeto tipado, porque lo que importa es qué bytes salen por el cable.
    /// </summary>
    [Fact]
    public async Task The_password_hash_is_not_exposed_by_the_api()
    {
        var (client, _, _) = await AuthenticateAsync();

        var raw = await (await client.GetAsync("/api/v1/auth/users/me")).Content.ReadAsStringAsync();

        raw.Should().NotContain("passwordHash", "el hash de la contraseña no tiene por qué llegar al cliente");
        raw.Should().NotContain("$2a$", "ni el hash en sí, se llame como se llame el campo");
    }
}
