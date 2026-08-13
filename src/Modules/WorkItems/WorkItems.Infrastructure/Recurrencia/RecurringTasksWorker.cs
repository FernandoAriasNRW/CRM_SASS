using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WorkItems.Infrastructure.Recurrencia;

/// <summary>
/// Despierta cada hora y crea las tareas recurrentes que tocan.
///
/// Cada hora y no cada pocos segundos porque la unidad más pequeña de una serie es el día:
/// mirar más a menudo sólo añadiría consultas. Y al arrancar se ejecuta una vez, para que una
/// aplicación que estuvo parada un fin de semana no espere otra hora a ponerse al día.
///
/// Un fallo aquí no puede tumbar el proceso: se registra y se reintenta en la siguiente vuelta,
/// igual que hace el worker del outbox.
/// </summary>
public sealed class RecurringTasksWorker(
    IServiceProvider serviceProvider,
    ILogger<RecurringTasksWorker> logger) : BackgroundService
{
  private static readonly TimeSpan Intervalo = TimeSpan.FromHours(1);

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    logger.LogInformation("Worker de tareas recurrentes iniciado");

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        using var scope = serviceProvider.CreateScope();
        var generador = scope.ServiceProvider.GetRequiredService<GeneradorDeTareasRecurrentes>();

        await generador.GenerarPendientesAsync(DateOnly.FromDateTime(DateTime.UtcNow), stoppingToken);
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Error generando tareas recurrentes");
      }

      try { await Task.Delay(Intervalo, stoppingToken); }
      catch (TaskCanceledException) { break; }
    }
  }
}
