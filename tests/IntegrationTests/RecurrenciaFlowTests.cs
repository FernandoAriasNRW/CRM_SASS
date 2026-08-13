using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorkItems.Infrastructure.Recurrencia;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Tareas recurrentes de punta a punta.
///
/// La prueba que de verdad importa aquí es la del generador: se ejecuta **fuera de una
/// petición**, como el worker, es decir sin usuario y por tanto sin tenant. El filtro global
/// cierra por defecto, así que un generador que no declarara `IgnoreQueryFilters` no vería ni
/// una serie y el worker daría vueltas cada hora sin crear nada **y sin dar un solo error**.
/// Eso ya pasó una vez en este proyecto con el claim del tenant, y no se detecta con pruebas de
/// dominio.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class RecurrenciaFlowTests(CrmApiFactory factory)
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

    private async Task<Guid> CrearTareaAsync(HttpClient cliente, Guid tenantId, Guid projectId, string titulo, DateOnly limite)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/tasks", new
        {
            tenantId,
            createdById = Guid.NewGuid(),
            projectId,
            title = titulo,
            description = "creada por las pruebas de integración",
            assigneeId = Guid.NewGuid(),
            estimatedHours = 1m,
            dueDate = limite.ToString("yyyy-MM-dd")
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>Ejecuta el generador como lo hace el worker: sin usuario y sin tenant.</summary>
    private async Task<int> GenerarComoElWorkerAsync(DateOnly hoy)
    {
        using var scope = factory.Services.CreateScope();
        var generador = scope.ServiceProvider.GetRequiredService<GeneradorDeTareasRecurrentes>();

        return await generador.GenerarPendientesAsync(hoy);
    }

    [Fact]
    public async Task Una_tarea_se_puede_marcar_como_recurrente_y_se_ve_al_leerla()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var tarea = await CrearTareaAsync(cliente, tenantId, Guid.NewGuid(), "Informe semanal", new DateOnly(2026, 9, 1));

        var puesta = await cliente.PutAsJsonAsync($"/api/v1/tasks/{tarea}/recurrence", new
        {
            frecuencia = "Semanal",
            intervalo = 1,
            proximaOcurrencia = "2026-09-01"
        });
        puesta.StatusCode.Should().Be(HttpStatusCode.OK);

        var leida = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{tarea}");
        var recurrencia = leida.GetProperty("recurrence");
        recurrencia.GetProperty("frecuencia").GetString().Should().Be("Semanal");
        recurrencia.GetProperty("intervalo").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task El_generador_ve_las_series_aunque_se_ejecute_sin_usuario()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var proyecto = Guid.NewGuid();
        var marca = $"serie-{Guid.NewGuid():N}";
        var tarea = await CrearTareaAsync(cliente, tenantId, proyecto, marca, new DateOnly(2026, 1, 5));

        await cliente.PutAsJsonAsync($"/api/v1/tasks/{tarea}/recurrence", new
        {
            frecuencia = "Diaria",
            intervalo = 1,
            proximaOcurrencia = "2026-01-05",
            fechaFin = "2026-01-07"
        });

        // Tres días pendientes: 5, 6 y 7. El worker se ejecuta sin usuario en contexto.
        var creadas = await GenerarComoElWorkerAsync(new DateOnly(2026, 1, 10));

        creadas.Should().Be(3, "sin IgnoreQueryFilters el generador no vería la serie y devolvería 0");

        var pagina = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks?projectId={proyecto}&pageSize=50");
        var limites = pagina.GetProperty("items").EnumerateArray()
            .Where(t => t.GetProperty("title").GetString() == marca)
            .Select(t => t.GetProperty("dueDate").GetString()!)
            .OrderBy(f => f)
            .ToList();

        // La plantilla más las tres ocurrencias.
        limites.Should().HaveCount(4);
        limites.Should().Contain(["2026-01-05", "2026-01-06", "2026-01-07"]);
    }

    [Fact]
    public async Task Las_tareas_generadas_pertenecen_al_tenant_de_su_plantilla()
    {
        // El generador cruza tenants para leer, pero lo que escribe tiene que quedar aislado: si
        // una ocurrencia naciera con el tenant vacío, sería invisible para todo el mundo.
        var (cliente, tenantId) = await AutenticarAsync();
        var proyecto = Guid.NewGuid();
        var marca = $"aislada-{Guid.NewGuid():N}";
        var tarea = await CrearTareaAsync(cliente, tenantId, proyecto, marca, new DateOnly(2026, 2, 2));

        await cliente.PutAsJsonAsync($"/api/v1/tasks/{tarea}/recurrence", new
        {
            frecuencia = "Diaria",
            intervalo = 1,
            proximaOcurrencia = "2026-02-02",
            fechaFin = "2026-02-02"
        });

        (await GenerarComoElWorkerAsync(new DateOnly(2026, 2, 5))).Should().Be(1);

        // Se consulta con el tenant del usuario: si la ocurrencia tuviera otro, no saldría.
        var pagina = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks?projectId={proyecto}&pageSize=50");
        pagina.GetProperty("items").EnumerateArray()
            .Count(t => t.GetProperty("title").GetString() == marca)
            .Should().Be(2, "la plantilla y su ocurrencia, las dos visibles para el tenant");
    }

    [Fact]
    public async Task Generar_dos_veces_no_duplica_las_ocurrencias()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var proyecto = Guid.NewGuid();
        var marca = $"idempotente-{Guid.NewGuid():N}";
        var tarea = await CrearTareaAsync(cliente, tenantId, proyecto, marca, new DateOnly(2026, 3, 3));

        await cliente.PutAsJsonAsync($"/api/v1/tasks/{tarea}/recurrence", new
        {
            frecuencia = "Diaria",
            intervalo = 1,
            proximaOcurrencia = "2026-03-03",
            fechaFin = "2026-03-04"
        });

        var primera = await GenerarComoElWorkerAsync(new DateOnly(2026, 3, 10));
        var segunda = await GenerarComoElWorkerAsync(new DateOnly(2026, 3, 10));

        primera.Should().Be(2);
        segunda.Should().Be(0, "la serie ya avanzó su próxima ocurrencia y quedó agotada");
    }

    [Fact]
    public async Task Dejar_de_repetir_detiene_la_serie()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var proyecto = Guid.NewGuid();
        var tarea = await CrearTareaAsync(cliente, tenantId, proyecto, $"parada-{Guid.NewGuid():N}", new DateOnly(2026, 4, 4));

        await cliente.PutAsJsonAsync($"/api/v1/tasks/{tarea}/recurrence", new
        {
            frecuencia = "Diaria", intervalo = 1, proximaOcurrencia = "2026-04-04"
        });

        var quitada = await cliente.DeleteAsync($"/api/v1/tasks/{tarea}/recurrence");
        quitada.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await GenerarComoElWorkerAsync(new DateOnly(2026, 4, 30))).Should().Be(0);
        (await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{tarea}"))
            .GetProperty("recurrence").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Una_frecuencia_inventada_se_rechaza()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var tarea = await CrearTareaAsync(cliente, tenantId, Guid.NewGuid(), "Rara", new DateOnly(2026, 5, 5));

        var respuesta = await cliente.PutAsJsonAsync($"/api/v1/tasks/{tarea}/recurrence", new
        {
            frecuencia = "Trimestral", intervalo = 1, proximaOcurrencia = "2026-05-05"
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await respuesta.Content.ReadAsStringAsync()).Should().Contain("Diaria, Semanal o Mensual");
    }
}
