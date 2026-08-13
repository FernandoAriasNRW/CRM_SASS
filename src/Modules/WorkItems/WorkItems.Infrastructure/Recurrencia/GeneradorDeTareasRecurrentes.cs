using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WorkItems.Infrastructure.Persistence;

namespace WorkItems.Infrastructure.Recurrencia;

/// <summary>
/// Crea las tareas que tocan de cada serie.
///
/// Va aparte del worker a propósito: así se puede ejecutar con una fecha concreta y comprobar
/// contra la base de datos lo que hace, en lugar de esperar a que salte un temporizador.
/// </summary>
public sealed class GeneradorDeTareasRecurrentes(
    WorkItemsDbContext context,
    ILogger<GeneradorDeTareasRecurrentes> logger)
{
  /// <summary>
  /// Genera lo pendiente hasta <paramref name="hoy"/> y devuelve cuántas tareas creó.
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
  public async Task<int> GenerarPendientesAsync(DateOnly hoy, CancellationToken ct = default)
  {
    var series = await context.Tasks
        .IgnoreQueryFilters()
        .Where(t => t.Recurrence != null && t.Recurrence.ProximaOcurrencia <= hoy)
        .ToListAsync(ct);

    if (series.Count == 0)
      return 0;

    var creadas = 0;

    foreach (var serie in series)
    {
      var ocurrencias = serie.GenerarOcurrenciasHasta(hoy);
      if (ocurrencias.Count == 0)
        continue;

      await context.Tasks.AddRangeAsync(ocurrencias, ct);
      creadas += ocurrencias.Count;
    }

    if (creadas > 0)
    {
      await context.SaveChangesAsync(ct);
      logger.LogInformation("Recurrencia: {Creadas} tareas creadas de {Series} series", creadas, series.Count);
    }

    return creadas;
  }
}
