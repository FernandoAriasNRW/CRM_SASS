using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// La checklist de una tarea, de punta a punta contra la API real.
///
/// Lo que sólo se ve aquí: que los puntos **vuelvan en el orden en que se escribieron**. El orden
/// de una checklist es del usuario, y una colección propiedad del agregado no vuelve ordenada de
/// la base: si la consulta se olvidara del ORDER BY, la lista saldría revuelta y nadie lo vería
/// en una prueba de dominio.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ChecklistFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    private async Task<(HttpClient cliente, Guid tenantId)> AutenticarAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var cuerpo = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        var relleno = cuerpo.PadRight(cuerpo.Length + (4 - cuerpo.Length % 4) % 4, '=');
        var tenantId = Guid.Parse(JsonDocument.Parse(Convert.FromBase64String(relleno))
            .RootElement.GetProperty("tenantId").GetString()!);

        return (cliente, tenantId);
    }

    private async Task<Guid> CrearTareaAsync(HttpClient cliente, Guid tenantId, string titulo)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/tasks", new
        {
            tenantId,
            createdById = Guid.NewGuid(),
            projectId = Guid.NewGuid(),
            title = titulo,
            description = "creada por las pruebas de integración",
            assigneeId = Guid.NewGuid(),
            estimatedHours = 1m,
            dueDate = "2026-12-01"
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> AgregarAsync(HttpClient cliente, Guid tarea, string texto)
    {
        var respuesta = await cliente.PostAsJsonAsync($"/api/v1/tasks/{tarea}/checklist", new { texto });
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<List<string>> TextosAsync(HttpClient cliente, Guid tarea)
    {
        var puntos = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{tarea}/checklist");
        return puntos.EnumerateArray().Select(p => p.GetProperty("texto").GetString()!).ToList();
    }

    [Fact]
    public async Task Los_puntos_vuelven_en_el_orden_en_que_se_escribieron()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var tarea = await CrearTareaAsync(cliente, tenantId, "Con checklist");

        foreach (var texto in new[] { "Comprar", "Cocinar", "Comer", "Recoger" })
            await AgregarAsync(cliente, tarea, texto);

        (await TextosAsync(cliente, tarea))
            .Should().ContainInOrder("Comprar", "Cocinar", "Comer", "Recoger");
    }

    [Fact]
    public async Task El_progreso_de_la_checklist_llega_en_la_tarea()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var tarea = await CrearTareaAsync(cliente, tenantId, "Con progreso");
        var uno = await AgregarAsync(cliente, tarea, "Uno");
        await AgregarAsync(cliente, tarea, "Dos");
        await AgregarAsync(cliente, tarea, "Tres");

        var marcado = await cliente.PatchAsJsonAsync($"/api/v1/tasks/{tarea}/checklist/{uno}", new { hecho = true });
        marcado.StatusCode.Should().Be(HttpStatusCode.OK);

        var leida = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{tarea}");
        leida.GetProperty("checklistTotal").GetInt32().Should().Be(3);
        leida.GetProperty("checklistDone").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Renombrar_un_punto_no_lo_desmarca()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var tarea = await CrearTareaAsync(cliente, tenantId, "Con typo");
        var punto = await AgregarAsync(cliente, tarea, "Con typo");
        await cliente.PatchAsJsonAsync($"/api/v1/tasks/{tarea}/checklist/{punto}", new { hecho = true });

        await cliente.PatchAsJsonAsync($"/api/v1/tasks/{tarea}/checklist/{punto}", new { texto = "Sin typo" });

        var puntos = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{tarea}/checklist");
        var unico = puntos.EnumerateArray().Single();
        unico.GetProperty("texto").GetString().Should().Be("Sin typo");
        unico.GetProperty("hecho").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Un_punto_sin_texto_se_rechaza()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var tarea = await CrearTareaAsync(cliente, tenantId, "Sin texto");

        var respuesta = await cliente.PostAsJsonAsync($"/api/v1/tasks/{tarea}/checklist", new { texto = "   " });

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await TextosAsync(cliente, tarea)).Should().BeEmpty();
    }

    [Fact]
    public async Task Borrar_del_medio_no_revuelve_el_orden_de_los_demas()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var tarea = await CrearTareaAsync(cliente, tenantId, "Con hueco");
        await AgregarAsync(cliente, tarea, "Primero");
        var delMedio = await AgregarAsync(cliente, tarea, "Segundo");
        await AgregarAsync(cliente, tarea, "Tercero");

        var borrado = await cliente.DeleteAsync($"/api/v1/tasks/{tarea}/checklist/{delMedio}");
        borrado.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await AgregarAsync(cliente, tarea, "Cuarto");

        (await TextosAsync(cliente, tarea)).Should().ContainInOrder("Primero", "Tercero", "Cuarto");
    }

    [Fact]
    public async Task Tocar_un_punto_que_no_existe_se_rechaza()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var tarea = await CrearTareaAsync(cliente, tenantId, "Vacía");

        var patch = await cliente.PatchAsJsonAsync($"/api/v1/tasks/{tarea}/checklist/{Guid.NewGuid()}", new { hecho = true });
        var borrado = await cliente.DeleteAsync($"/api/v1/tasks/{tarea}/checklist/{Guid.NewGuid()}");

        patch.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        borrado.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
