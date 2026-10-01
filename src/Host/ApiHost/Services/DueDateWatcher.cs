using Automations.Application.Abstractions;
using Automations.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using WorkItems.Infrastructure.Persistence;
using TaskStatus = WorkItems.Domain.ValueObjects.TaskStatus;

namespace ApiHost.Services;

/// <summary>
/// Revisa una vez al día qué tareas se acercan a su vencimiento y dispara las automatizaciones
/// que correspondan.
///
/// **Vive en el host porque cruza módulos**: lee tareas de WorkItems y llama al motor de
/// Automations, y ningún módulo referencia a otro. Mismo sitio y mismo motivo que
/// <see cref="AutomationsBridge"/>.
///
/// Es el disparador que reacciona a que **no** ha pasado nada. Los otros tres responden a algo
/// que alguien hizo —crear, mover, repriorizar—; una tarea que se acerca a su fecha sin que
/// nadie la toque no emite ningún evento, y es justo el caso que hay que vigilar.
/// </summary>
public sealed class DueDateWatcher(
    IServiceProvider serviceProvider,
    ILogger<DueDateWatcher> logger) : BackgroundService
{
    /// <summary>
    /// Cada hora, no cada día.
    ///
    /// Lo que se comprueba es diario —«faltan dos días»— pero el intervalo es más corto a
    /// propósito: con un ciclo de 24 horas, un despliegue a las 23:50 dejaría un día entero sin
    /// revisar y nadie lo notaría hasta que un aviso no llegara. Repetir dentro del mismo día no
    /// cuesta nada porque el registro de ejecuciones hace de memoria.
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    /// <summary>
    /// Cuántos días hacia adelante se miran.
    ///
    /// Una tarea que vence dentro de un año no interesa a ninguna regla razonable, y traerlas
    /// todas convertiría esto en un recorrido de la tabla entera cada hora. Treinta días cubre
    /// de sobra los avisos que la gente configura.
    /// </summary>
    private const int DaysAhead = 30;

    /// <summary>
    /// Y cuántos hacia atrás. Una tarea que venció hace medio año y sigue abierta ya no necesita
    /// que se avise otra vez: o se abandonó, o el aviso lleva medio año sin surtir efecto.
    /// </summary>
    private const int DaysBehind = 30;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Vigilante de vencimientos iniciado");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // Un fallo no puede tumbar el trabajo: si el bucle muere, deja de haber avisos
                // para siempre y nada lo dice. Se registra y se vuelve a intentar dentro de una
                // hora.
                logger.LogError(ex, "Error revisando vencimientos");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task CheckAsync(CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();

        var tasksDb = scope.ServiceProvider.GetRequiredService<WorkItemsDbContext>();
        var motor = scope.ServiceProvider.GetRequiredService<IAutomationEngine>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = today.AddDays(-DaysBehind);
        var to = today.AddDays(DaysAhead);

        // `IgnoreQueryFilters` porque esto no corre dentro de una petición: no hay usuario, así
        // que el filtro global de inquilino dejaría la consulta vacía y el trabajo no haría nada
        // sin dar ningún error. El inquilino se lleva a mano en cada disparo, que es lo que
        // mantiene el aislamiento aquí.
        var candidates = await tasksDb.Tasks
            .IgnoreQueryFilters()
            .AsNoTracking()
            // `CompletedAtUtc == null` descarta lo terminado sin tener que interpretar el
            // estado en SQL. No hay filtro de borrado lógico porque WorkTask no lo tiene: las
            // tareas se borran de verdad.
            .Where(t => t.DueDate >= from
                     && t.DueDate <= to
                     && t.CompletedAtUtc == null)
            .Select(t => new
            {
                t.Id,
                t.TenantId,
                t.ProjectId,
                t.DueDate,
                Title = t.Title.Value,
                Status = t.Status.Value,
                Priority = t.Priority.Value,
                t.AssigneeId,
            })
            .ToListAsync(ct);

        if (candidates.Count == 0) return;

        var triggerEvents = 0;

        foreach (var task in candidates)
        {
            // Terminada no vence. Se filtra aquí y no en la consulta porque el estado final se
            // reconoce por valor o por nombre —ver TaskStatus.EsFinal— y esa comparación no se
            // traduce a SQL.
            if (TaskStatus.IsFinal(task.Status)) continue;

            var data = TriggerData(
                today, task.DueDate, task.Status, task.Priority,
                task.ProjectId, task.AssigneeId, task.Title);

            triggerEvents += await motor.RunAsync(new AutomationTriggerEvent(
                task.TenantId, TriggerTypes.TaskDueSoon, task.Id, data), ct);
        }

        if (triggerEvents > 0)
            logger.LogInformation(
                "Vencimientos: {Disparos} automatizaciones aplicadas sobre {Candidatas} tareas",
                triggerEvents, candidates.Count);
    }

    /// <summary>
    /// Los datos del disparo. Son exactamente los que declara
    /// <see cref="EventFields.ByTrigger"/> para <see cref="TriggerTypes.TaskDueSoon"/>, y está
    /// aparte para que una prueba lo compruebe sin base de datos.
    /// </summary>
    public static Dictionary<string, string?> TriggerData(
        DateOnly today, DateOnly dueDate, string status, string priority,
        Guid projectId, Guid assigneeId, string title) => new()
    {
        // Negativo si ya venció, 0 si vence hoy. Es lo que compara la condición.
        [EventFields.DaysUntilDue] = (dueDate.DayNumber - today.DayNumber).ToString(),
        [EventFields.Status] = status,
        [EventFields.Priority] = priority,
        [EventFields.ProjectId] = projectId.ToString(),
        [EventFields.AssigneeId] = assigneeId == Guid.Empty ? null : assigneeId.ToString(),
        [EventFields.Title] = title,
    };
}
