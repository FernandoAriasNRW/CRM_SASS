using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WorkItems.Infrastructure.Persistence;

namespace WorkItems.Infrastructure.Recurrence;

/// <summary>
/// Crea las tareas que tocan de cada serie.
///
/// Va aparte del worker a propósito: así se puede ejecutar con una fecha concreta y comprobar
/// contra la base de datos lo que hace, en lugar de esperar a que salte un temporizador.
/// </summary>
public sealed class RecurringTaskGenerator(
    WorkItemsDbContext context,
    ILogger<RecurringTaskGenerator> logger)
{
  /// <summary>
  /// Genera lo pendiente hasta <paramref name="today"/> y devuelve cuántas tareas creó.
  ///
  /// **Cruza tenants a propósito y lo declara.** El filtro global cierra por defecto: sin
  /// usuario en contexto el tenant es <c>Guid.Empty</c> y esta consulta no vería ni una serie,
  /// así que el worker se ejecutaría cada hora sin hacer nada y sin dar un solo error. Es
  /// exactamente el escenario para el que el ADR-0004 admite <c>IgnoreQueryFilters</c>: un
  /// proceso de fondo que legítimamente trabaja para todos los clientes.
  ///
  /// Cada tarea generada lleva el <c>TenantId</c> de su plantilla, así que el aislamiento se
  /// mantiene en lo que se escribe.
  /// </summary>
  public async Task<int> GeneratePendingAsync(DateOnly today, CancellationToken ct = default)
  {
    var dueSeries = await context.Tasks
        .IgnoreQueryFilters()
        .Where(t => t.Recurrence != null && t.Recurrence.NextOccurrence <= today)
        .ToListAsync(ct);

    if (dueSeries.Count == 0)
      return 0;

    var created = 0;

    foreach (var series in dueSeries)
    {
      var occurrences = series.GenerateOccurrencesUntil(today);
      if (occurrences.Count == 0)
        continue;

      await context.Tasks.AddRangeAsync(occurrences, ct);
      created += occurrences.Count;
    }

    if (created > 0)
    {
      await context.SaveChangesAsync(ct);
      logger.LogInformation("Recurrencia: {Created} tareas creadas de {Series} series", created, dueSeries.Count);
    }

    return created;
  }
}
