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
public sealed class AislamientoEntreInquilinosTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    private async Task<(HttpClient Cliente, Guid Tenant, Guid Usuario)> AutenticarAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var yo = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/auth/users/me");
        return (cliente, yo.GetProperty("tenantId").GetGuid(), yo.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Un_proyecto_se_guarda_en_mi_inquilino_aunque_el_cuerpo_diga_otro()
    {
        var (cliente, miTenant, yo) = await AutenticarAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/projects", new
        {
            tenantId = Guid.NewGuid(),   // no es el mío
            ownerId = Guid.NewGuid(),    // tampoco soy yo
            spaceId = Guid.NewGuid(),
            name = "Proyecto con inquilino ajeno en el cuerpo",
            description = "Debe acabar en mi inquilino, no en el que pide el JSON",
            estimatedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);

        var creado = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        creado.GetProperty("tenantId").GetGuid().Should().Be(miTenant,
            "el inquilino sale del token; si sale del cuerpo, cualquiera escribe en los datos de otra empresa");
        creado.GetProperty("ownerId").GetGuid().Should().Be(yo,
            "el dueño es quien lo crea, no quien diga la petición");
    }

    /// <summary>
    /// La otra cara del mismo fallo: si el alta guarda con un inquilino que no es el mío, el
    /// listado —que sí filtra— no lo encuentra. Un 201 seguido de una lista vacía.
    /// </summary>
    [Fact]
    public async Task Un_proyecto_recien_creado_aparece_en_el_listado()
    {
        var (cliente, _, _) = await AutenticarAsync();
        var nombre = "Proyecto visible " + Guid.NewGuid();

        var alta = await cliente.PostAsJsonAsync("/api/v1/projects", new
        {
            spaceId = Guid.NewGuid(),
            name = nombre,
            description = "Tiene que salir en el listado justo después de crearlo",
            estimatedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
        });
        alta.StatusCode.Should().Be(HttpStatusCode.Created);

        var listado = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/projects?pageSize=100");
        var nombres = listado.GetProperty("items").EnumerateArray()
            .Select(p => p.GetProperty("name").ValueKind == JsonValueKind.Object
                ? p.GetProperty("name").GetProperty("value").GetString()
                : p.GetProperty("name").GetString())
            .ToList();

        nombres.Should().Contain(nombre,
            "un alta que responde 201 y luego no se ve es peor que un error: nadie sabe que se perdió");
    }

    [Fact]
    public async Task Una_tarea_se_guarda_en_mi_inquilino_aunque_el_cuerpo_diga_otro()
    {
        var (cliente, miTenant, yo) = await AutenticarAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/tasks", new
        {
            tenantId = Guid.NewGuid(),
            createdById = Guid.NewGuid(),
            projectId = Guid.NewGuid(),
            title = "Tarea con inquilino ajeno en el cuerpo",
            description = "Debe acabar en mi inquilino",
            assigneeId = Guid.Empty,
            estimatedHours = 1,
            dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);

        var creada = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        creada.GetProperty("tenantId").GetGuid().Should().Be(miTenant);
        creada.GetProperty("createdById").GetGuid().Should().Be(yo);
    }

    /// <summary>
    /// Una suscripción de webhook plantada en otra organización recibiría sus eventos en una
    /// URL elegida por quien la plantó. De los que había, era el más caro.
    /// </summary>
    [Fact]
    public async Task Un_webhook_se_guarda_en_mi_inquilino_aunque_el_cuerpo_diga_otro()
    {
        var (cliente, miTenant, _) = await AutenticarAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/webhooks", new
        {
            tenantId = Guid.NewGuid(),
            targetUrl = "https://ejemplo.invalido/hook",
            eventName = "task.created",
            secret = "un-secreto-de-prueba-suficientemente-largo",
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);

        var creado = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        creado.GetProperty("tenantId").GetGuid().Should().Be(miTenant);
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
    public async Task El_tablero_mueve_una_tarea_con_la_llamada_que_hace_de_verdad()
    {
        var (cliente, _, _) = await AutenticarAsync();
        var tarea = await CrearTareaAsync(cliente);

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/v1/tasks/{tarea}/move", new { newStatus = "In Progress" });

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK,
            "es exactamente la petición que manda el tablero al soltar una tarjeta");

        var recargada = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{tarea}");
        var estado = recargada.GetProperty("status");
        var valor = estado.ValueKind == JsonValueKind.Object
            ? estado.GetProperty("value").GetString()
            : estado.GetString();

        valor.Should().Be("In Progress",
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
    public async Task El_rol_que_viaja_en_la_url_no_decide_nada()
    {
        var (cliente, _, _) = await AutenticarAsync();
        var tarea = await CrearTareaAsync(cliente);

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/v1/tasks/{tarea}/move?actorRole=Guest&actorId={Guid.NewGuid()}",
            new { newStatus = "In Progress" });

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK,
            "quien manda es el token; lo que diga la URL sobre el rol es ruido y debe ignorarse");
    }

    private static async Task<Guid> CrearTareaAsync(HttpClient cliente)
    {
        var alta = await cliente.PostAsJsonAsync("/api/v1/tasks", new
        {
            projectId = Guid.NewGuid(),
            title = "Tarea para mover",
            description = "Creada por la prueba",
            assigneeId = Guid.Empty,
            estimatedHours = 2,
            dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
        });
        alta.EnsureSuccessStatusCode();
        return (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>
    /// El hash de la contraseña no sale de la API. Salía: <c>GET /auth/users/me</c> devolvía el
    /// DTO del usuario con el bcrypt dentro, así que viajaba al navegador en cada arranque y se
    /// quedaba por el camino en cachés y registros. Se comprueba sobre el texto crudo, no sobre
    /// un objeto tipado, porque lo que importa es qué bytes salen por el cable.
    /// </summary>
    [Fact]
    public async Task El_hash_de_la_contrasena_no_sale_por_la_api()
    {
        var (cliente, _, _) = await AutenticarAsync();

        var crudo = await (await cliente.GetAsync("/api/v1/auth/users/me")).Content.ReadAsStringAsync();

        crudo.Should().NotContain("passwordHash", "el hash de la contraseña no tiene por qué llegar al cliente");
        crudo.Should().NotContain("$2a$", "ni el hash en sí, se llame como se llame el campo");
    }
}
