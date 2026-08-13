using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// La edición suelta de una tarea, de punta a punta contra la API real.
///
/// Existen porque el `PATCH` **aceptaba el cambio y no lo guardaba**: el handler sólo aplicaba
/// responsable, estado y prioridad, e ignoraba en silencio el título, la descripción y la fecha;
/// las horas ni siquiera estaban en el comando. Devolvía 200 igualmente, así que la pantalla
/// decía «guardado», el usuario se iba tranquilo y al recargar volvía el valor viejo.
///
/// La lección que fija esta clase: **una prueba que sólo mire el código de estado no habría visto
/// nada**. Cada caso vuelve a pedir la tarea y comprueba el valor.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TaskPatchFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    private async Task<(HttpClient cliente, Guid tenantId)> AutenticarAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var cuerpo = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = cuerpo.GetProperty("accessToken").GetString()!;

        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);

        return (cliente, TenantDelToken(token));
    }

    private static Guid TenantDelToken(string token)
    {
        var cuerpo = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        var relleno = cuerpo.PadRight(cuerpo.Length + (4 - cuerpo.Length % 4) % 4, '=');
        var json = JsonDocument.Parse(Convert.FromBase64String(relleno));

        return Guid.Parse(json.RootElement.GetProperty("tenantId").GetString()!);
    }

    private async Task<Guid> CrearAsync(HttpClient cliente, Guid tenantId, string titulo)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/tasks", new
        {
            tenantId,
            createdById = Guid.NewGuid(),
            projectId = Guid.NewGuid(),
            title = titulo,
            description = "creada por las pruebas de integración",
            assigneeId = Guid.NewGuid(),
            estimatedHours = 8m,
            dueDate = "2026-12-01",
            priority = "Normal"
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
        var creada = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return creada.GetProperty("id").GetGuid();
    }

    private Task<JsonElement> LeerAsync(HttpClient cliente, Guid id) =>
        cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{id}");

    [Fact]
    public async Task El_titulo_se_cambia_y_persiste()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var id = await CrearAsync(cliente, tenantId, "Título original");

        var patch = await cliente.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { title = "Título corregido" });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var recuperada = await LeerAsync(cliente, id);
        recuperada.GetProperty("title").GetString().Should().Be("Título corregido");
    }

    [Fact]
    public async Task Las_horas_estimadas_se_cambian_y_persisten()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var id = await CrearAsync(cliente, tenantId, "Para reestimar");

        var patch = await cliente.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { estimatedHours = 13.5m });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var recuperada = await LeerAsync(cliente, id);
        recuperada.GetProperty("estimatedHours").GetDecimal().Should().Be(13.5m);
    }

    [Fact]
    public async Task La_fecha_limite_se_cambia_y_persiste()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var id = await CrearAsync(cliente, tenantId, "Para reprogramar");

        var patch = await cliente.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { dueDate = "2027-03-15" });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var recuperada = await LeerAsync(cliente, id);
        recuperada.GetProperty("dueDate").GetString().Should().StartWith("2027-03-15");
    }

    [Fact]
    public async Task La_descripcion_se_cambia_y_persiste()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var id = await CrearAsync(cliente, tenantId, "Para redescribir");

        var patch = await cliente.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { description = "otra descripción" });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var recuperada = await LeerAsync(cliente, id);
        recuperada.GetProperty("description").GetString().Should().Be("otra descripción");
    }

    /// <summary>
    /// La tabla y el detalle mandan sólo el campo que cambió. Si lo ausente se tomara como
    /// «déjalo vacío», corregir una fecha borraría el título.
    /// </summary>
    [Fact]
    public async Task Cambiar_un_campo_no_toca_los_demas()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var id = await CrearAsync(cliente, tenantId, "Título que debe sobrevivir");

        await cliente.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { estimatedHours = 3m });

        var recuperada = await LeerAsync(cliente, id);
        recuperada.GetProperty("title").GetString().Should().Be("Título que debe sobrevivir");
        recuperada.GetProperty("description").GetString().Should().Be("creada por las pruebas de integración");
        recuperada.GetProperty("dueDate").GetString().Should().StartWith("2026-12-01");
    }

    [Fact]
    public async Task Varios_campos_a_la_vez_se_guardan_todos()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var id = await CrearAsync(cliente, tenantId, "Para editar entero");

        var patch = await cliente.PatchAsJsonAsync($"/api/v1/tasks/{id}", new
        {
            title = "Editado del todo",
            status = "In Progress",
            priority = "High",
            estimatedHours = 21m,
            dueDate = "2027-01-31"
        });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var recuperada = await LeerAsync(cliente, id);
        recuperada.GetProperty("title").GetString().Should().Be("Editado del todo");
        recuperada.GetProperty("status").GetString().Should().Be("In Progress");
        recuperada.GetProperty("priority").GetString().Should().Be("High");
        recuperada.GetProperty("estimatedHours").GetDecimal().Should().Be(21m);
        recuperada.GetProperty("dueDate").GetString().Should().StartWith("2027-01-31");
    }

    [Fact]
    public async Task Un_titulo_vacio_se_rechaza_y_no_llega_a_la_base()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var id = await CrearAsync(cliente, tenantId, "Título que no debe perderse");

        var patch = await cliente.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { title = "   " });
        patch.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var recuperada = await LeerAsync(cliente, id);
        recuperada.GetProperty("title").GetString().Should().Be("Título que no debe perderse");
    }

    [Fact]
    public async Task Unas_horas_negativas_se_rechazan_y_no_llegan_a_la_base()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var id = await CrearAsync(cliente, tenantId, "Con horas sanas");

        var patch = await cliente.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { estimatedHours = -5m });
        patch.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var recuperada = await LeerAsync(cliente, id);
        recuperada.GetProperty("estimatedHours").GetDecimal().Should().Be(8m);
    }

    /// <summary>
    /// Un valor rechazado no es una tarea que no existe. Devolver 404 mandaría a buscar el fallo
    /// donde no está, y la pantalla no podría distinguir «se borró» de «no vale».
    /// </summary>
    [Fact]
    public async Task La_fecha_de_inicio_se_guarda_al_crear_y_vuelve_al_leer()
    {
        var (cliente, tenantId) = await AutenticarAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/tasks", new
        {
            tenantId,
            createdById = Guid.NewGuid(),
            projectId = Guid.NewGuid(),
            title = "Con calendario",
            description = "creada por las pruebas de integración",
            assigneeId = Guid.NewGuid(),
            estimatedHours = 8m,
            dueDate = "2026-12-01",
            startDate = "2026-11-25",
            priority = "Normal"
        });
        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);

        var id = (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var recuperada = await LeerAsync(cliente, id);

        recuperada.GetProperty("startDate").GetString().Should().StartWith("2026-11-25");
    }

    /// <summary>
    /// Una tarea sin inicio lo devuelve nulo y no una fecha inventada: el Gantt la pinta como un
    /// hito en su vencimiento, que es lo único que de verdad se sabe.
    /// </summary>
    [Fact]
    public async Task Una_tarea_sin_fecha_de_inicio_la_devuelve_nula()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var id = await CrearAsync(cliente, tenantId, "Sin calendario");

        var recuperada = await LeerAsync(cliente, id);

        recuperada.GetProperty("startDate").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task La_fecha_de_inicio_se_pone_y_se_quita()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var id = await CrearAsync(cliente, tenantId, "Para planificar");

        await cliente.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { startDate = "2026-11-20" });
        (await LeerAsync(cliente, id)).GetProperty("startDate").GetString().Should().StartWith("2026-11-20");

        // `null` significa «no toques este campo», así que vaciarla necesita su propio
        // interruptor. Sin él no habría forma de quitarla desde una pantalla que manda parches.
        await cliente.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { quitarFechaInicio = true });
        (await LeerAsync(cliente, id)).GetProperty("startDate").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Un_inicio_posterior_al_vencimiento_se_rechaza_y_no_llega_a_la_base()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var id = await CrearAsync(cliente, tenantId, "Con vencimiento en diciembre");

        var patch = await cliente.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { startDate = "2027-01-01" });
        patch.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await LeerAsync(cliente, id)).GetProperty("startDate").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Mover_las_dos_fechas_a_la_vez_hacia_adelante_vale()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var id = await CrearAsync(cliente, tenantId, "Para reprogramar entera");
        await cliente.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { startDate = "2026-11-25" });

        var patch = await cliente.PatchAsJsonAsync($"/api/v1/tasks/{id}",
            new { startDate = "2027-01-05", dueDate = "2027-01-10" });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var recuperada = await LeerAsync(cliente, id);
        recuperada.GetProperty("startDate").GetString().Should().StartWith("2027-01-05");
        recuperada.GetProperty("dueDate").GetString().Should().StartWith("2027-01-10");
    }

    [Fact]
    public async Task Una_tarea_que_no_existe_da_404_y_un_valor_invalido_da_400()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var id = await CrearAsync(cliente, tenantId, "Existe");

        var inexistente = await cliente.PatchAsJsonAsync($"/api/v1/tasks/{Guid.NewGuid()}", new { title = "Da igual" });
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var invalida = await cliente.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { priority = "Altísima" });
        invalida.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
