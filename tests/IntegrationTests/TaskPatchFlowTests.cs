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

    private async Task<(HttpClient client, Guid tenantId)> AuthenticateAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        return (client, TenantFromToken(token));
    }

    private static Guid TenantFromToken(string token)
    {
        var body = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        var payload = body.PadRight(body.Length + (4 - body.Length % 4) % 4, '=');
        var json = JsonDocument.Parse(Convert.FromBase64String(payload));

        return Guid.Parse(json.RootElement.GetProperty("tenantId").GetString()!);
    }

    private async Task<Guid> CreateAsync(HttpClient client, Guid tenantId, string title)
    {
        var response = await client.PostAsJsonAsync("/api/v1/tasks", new
        {
            tenantId,
            createdById = Guid.NewGuid(),
            projectId = await TestProjects.CreateAsync(client),
            title = title,
            description = "creada por las pruebas de integración",
            assigneeId = Guid.NewGuid(),
            estimatedHours = 8m,
            dueDate = "2026-12-01",
            priority = "Normal"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }

    private Task<JsonElement> ReadAsync(HttpClient client, Guid id) =>
        client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{id}");

    [Fact]
    public async Task The_title_changes_and_persists()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var id = await CreateAsync(client, tenantId, "Título original");

        var patch = await client.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { title = "Título corregido" });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var retrieved = await ReadAsync(client, id);
        retrieved.GetProperty("title").GetString().Should().Be("Título corregido");
    }

    [Fact]
    public async Task Estimated_hours_change_and_persist()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var id = await CreateAsync(client, tenantId, "Para reestimar");

        var patch = await client.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { estimatedHours = 13.5m });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var retrieved = await ReadAsync(client, id);
        retrieved.GetProperty("estimatedHours").GetDecimal().Should().Be(13.5m);
    }

    [Fact]
    public async Task The_due_date_changes_and_persists()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var id = await CreateAsync(client, tenantId, "Para reprogramar");

        var patch = await client.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { dueDate = "2027-03-15" });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var retrieved = await ReadAsync(client, id);
        retrieved.GetProperty("dueDate").GetString().Should().StartWith("2027-03-15");
    }

    [Fact]
    public async Task The_description_changes_and_persists()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var id = await CreateAsync(client, tenantId, "Para redescribir");

        var patch = await client.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { description = "otra descripción" });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var retrieved = await ReadAsync(client, id);
        retrieved.GetProperty("description").GetString().Should().Be("otra descripción");
    }

    /// <summary>
    /// La tabla y el detalle mandan sólo el campo que cambió. Si lo ausente se tomara como
    /// «déjalo vacío», corregir una fecha borraría el título.
    /// </summary>
    [Fact]
    public async Task Changing_one_field_keeps_the_others()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var id = await CreateAsync(client, tenantId, "Título que debe sobrevivir");

        await client.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { estimatedHours = 3m });

        var retrieved = await ReadAsync(client, id);
        retrieved.GetProperty("title").GetString().Should().Be("Título que debe sobrevivir");
        retrieved.GetProperty("description").GetString().Should().Be("creada por las pruebas de integración");
        retrieved.GetProperty("dueDate").GetString().Should().StartWith("2026-12-01");
    }

    [Fact]
    public async Task Several_fields_at_once_are_all_saved()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var id = await CreateAsync(client, tenantId, "Para editar entero");

        var patch = await client.PatchAsJsonAsync($"/api/v1/tasks/{id}", new
        {
            title = "Editado del todo",
            status = "In Progress",
            priority = "High",
            estimatedHours = 21m,
            dueDate = "2027-01-31"
        });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var retrieved = await ReadAsync(client, id);
        retrieved.GetProperty("title").GetString().Should().Be("Editado del todo");
        retrieved.GetProperty("status").GetString().Should().Be("In Progress");
        retrieved.GetProperty("priority").GetString().Should().Be("High");
        retrieved.GetProperty("estimatedHours").GetDecimal().Should().Be(21m);
        retrieved.GetProperty("dueDate").GetString().Should().StartWith("2027-01-31");
    }

    [Fact]
    public async Task An_empty_title_is_rejected_and_never_stored()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var id = await CreateAsync(client, tenantId, "Título que no debe perderse");

        var patch = await client.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { title = "   " });
        patch.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var retrieved = await ReadAsync(client, id);
        retrieved.GetProperty("title").GetString().Should().Be("Título que no debe perderse");
    }

    [Fact]
    public async Task Negative_hours_are_rejected_and_never_stored()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var id = await CreateAsync(client, tenantId, "Con horas sanas");

        var patch = await client.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { estimatedHours = -5m });
        patch.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var retrieved = await ReadAsync(client, id);
        retrieved.GetProperty("estimatedHours").GetDecimal().Should().Be(8m);
    }

    /// <summary>
    /// Un valor rechazado no es una tarea que no existe. Devolver 404 mandaría a buscar el fallo
    /// donde no está, y la pantalla no podría distinguir «se borró» de «no vale».
    /// </summary>
    [Fact]
    public async Task The_start_date_is_saved_on_create_and_returned_on_read()
    {
        var (client, tenantId) = await AuthenticateAsync();

        var response = await client.PostAsJsonAsync("/api/v1/tasks", new
        {
            tenantId,
            createdById = Guid.NewGuid(),
            projectId = await TestProjects.CreateAsync(client),
            title = "Con calendario",
            description = "creada por las pruebas de integración",
            assigneeId = Guid.NewGuid(),
            estimatedHours = 8m,
            dueDate = "2026-12-01",
            startDate = "2026-11-25",
            priority = "Normal"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var retrieved = await ReadAsync(client, id);

        retrieved.GetProperty("startDate").GetString().Should().StartWith("2026-11-25");
    }

    /// <summary>
    /// Una tarea sin inicio lo devuelve nulo y no una fecha inventada: el Gantt la pinta como un
    /// hito en su vencimiento, que es lo único que de verdad se sabe.
    /// </summary>
    [Fact]
    public async Task A_task_without_start_date_returns_null()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var id = await CreateAsync(client, tenantId, "Sin calendario");

        var retrieved = await ReadAsync(client, id);

        retrieved.GetProperty("startDate").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task The_start_date_can_be_set_and_cleared()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var id = await CreateAsync(client, tenantId, "Para planificar");

        await client.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { startDate = "2026-11-20" });
        (await ReadAsync(client, id)).GetProperty("startDate").GetString().Should().StartWith("2026-11-20");

        // `null` significa «no toques este campo», así que vaciarla necesita su propio
        // interruptor. Sin él no habría forma de quitarla desde una pantalla que manda parches.
        await client.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { clearStartDate = true });
        (await ReadAsync(client, id)).GetProperty("startDate").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task A_start_after_the_due_date_is_rejected_and_never_stored()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var id = await CreateAsync(client, tenantId, "Con vencimiento en diciembre");

        var patch = await client.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { startDate = "2027-01-01" });
        patch.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await ReadAsync(client, id)).GetProperty("startDate").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Moving_both_dates_forward_together_is_valid()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var id = await CreateAsync(client, tenantId, "Para reprogramar entera");
        await client.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { startDate = "2026-11-25" });

        var patch = await client.PatchAsJsonAsync($"/api/v1/tasks/{id}",
            new { startDate = "2027-01-05", dueDate = "2027-01-10" });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var retrieved = await ReadAsync(client, id);
        retrieved.GetProperty("startDate").GetString().Should().StartWith("2027-01-05");
        retrieved.GetProperty("dueDate").GetString().Should().StartWith("2027-01-10");
    }

    [Fact]
    public async Task A_missing_task_returns_404_and_an_invalid_value_400()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var id = await CreateAsync(client, tenantId, "Existe");

        var missing = await client.PatchAsJsonAsync($"/api/v1/tasks/{Guid.NewGuid()}", new { title = "Da igual" });
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var invalid = await client.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { priority = "Altísima" });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
