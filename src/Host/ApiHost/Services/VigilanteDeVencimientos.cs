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
/// <see cref="PuenteDeAutomatizaciones"/>.
///
/// Es el disparador que reacciona a que **no** ha pasado nada. Los otros tres responden a algo
/// que alguien hizo —crear, mover, repriorizar—; una tarea que se acerca a su fecha sin que
/// nadie la toque no emite ningún evento, y es justo el caso que hay que vigilar.
/// </summary>
public sealed class VigilanteDeVencimientos(
    IServiceProvider serviceProvider,
    ILogger<VigilanteDeVencimientos> logger) : BackgroundService
{
    /// <summary>
    /// Cada hora, no cada día.
    ///
    /// Lo que se comprueba es diario —«faltan dos días»— pero el intervalo es más corto a
    /// propósito: con un ciclo de 24 horas, un despliegue a las 23:50 dejaría un día entero sin
    /// revisar y nadie lo notaría hasta que un aviso no llegara. Repetir dentro del mismo día no
    /// cuesta nada porque el registro de ejecuciones hace de memoria.
    /// </summary>
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(1);

    /// <summary>
    /// Cuántos días hacia adelante se miran.
    ///
    /// Una tarea que vence dentro de un año no interesa a ninguna regla razonable, y traerlas
    /// todas convertiría esto en un recorrido de la tabla entera cada hora. Treinta días cubre
    /// de sobra los avisos que la gente configura.
    /// </summary>
    private const int DiasHaciaAdelante = 30;

    /// <summary>
    /// Y cuántos hacia atrás. Una tarea que venció hace medio año y sigue abierta ya no necesita
    /// que se avise otra vez: o se abandonó, o el aviso lleva medio año sin surtir efecto.
    /// </summary>
    private const int DiasHaciaAtras = 30;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Vigilante de vencimientos iniciado");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RevisarAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // Un fallo no puede tumbar el trabajo: si el bucle muere, deja de haber avisos
                // para siempre y nada lo dice. Se registra y se vuelve a intentar dentro de una
                // hora.
                logger.LogError(ex, "Error revisando vencimientos");
            }

            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task RevisarAsync(CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();

        var tareasDb = scope.ServiceProvider.GetRequiredService<WorkItemsDbContext>();
        var motor = scope.ServiceProvider.GetRequiredService<IMotorDeAutomatizaciones>();

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var desde = hoy.AddDays(-DiasHaciaAtras);
        var hasta = hoy.AddDays(DiasHaciaAdelante);

        // `IgnoreQueryFilters` porque esto no corre dentro de una petición: no hay usuario, así
        // que el filtro global de inquilino dejaría la consulta vacía y el trabajo no haría nada
        // sin dar ningún error. El inquilino se lleva a mano en cada disparo, que es lo que
        // mantiene el aislamiento aquí.
        var candidatas = await tareasDb.Tasks
            .IgnoreQueryFilters()
            .AsNoTracking()
            // `CompletedAtUtc == null` descarta lo terminado sin tener que interpretar el
            // estado en SQL. No hay filtro de borrado lógico porque WorkTask no lo tiene: las
            // tareas se borran de verdad.
            .Where(t => t.DueDate >= desde
                     && t.DueDate <= hasta
                     && t.CompletedAtUtc == null)
            .Select(t => new
            {
                t.Id,
                t.TenantId,
                t.ProjectId,
                t.DueDate,
                Titulo = t.Title.Value,
                Estado = t.Status.Value,
                Prioridad = t.Priority.Value,
                t.AssigneeId,
            })
            .ToListAsync(ct);

        if (candidatas.Count == 0) return;

        var disparos = 0;

        foreach (var tarea in candidatas)
        {
            // Terminada no vence. Se filtra aquí y no en la consulta porque el estado final se
            // reconoce por valor o por nombre —ver TaskStatus.EsFinal— y esa comparación no se
            // traduce a SQL.
            if (TaskStatus.EsFinal(tarea.Estado)) continue;

            var datos = new Dictionary<string, string?>
            {
                // Negativo si ya venció, 0 si vence hoy. Es lo que compara la condición.
                [CampoDelEvento.DiasParaVencer] =
                    (tarea.DueDate.DayNumber - hoy.DayNumber).ToString(),
                [CampoDelEvento.Estado] = tarea.Estado,
                [CampoDelEvento.Prioridad] = tarea.Prioridad,
                [CampoDelEvento.ProyectoId] = tarea.ProjectId.ToString(),
                [CampoDelEvento.ResponsableId] =
                    tarea.AssigneeId == Guid.Empty ? null : tarea.AssigneeId.ToString(),
                [CampoDelEvento.Titulo] = tarea.Titulo,
            };

            disparos += await motor.EjecutarAsync(new DisparoDeAutomatizacion(
                tarea.TenantId, TipoDeDisparador.TareaPorVencer, tarea.Id, datos), ct);
        }

        if (disparos > 0)
            logger.LogInformation(
                "Vencimientos: {Disparos} automatizaciones aplicadas sobre {Candidatas} tareas",
                disparos, candidatas.Count);
    }
}
