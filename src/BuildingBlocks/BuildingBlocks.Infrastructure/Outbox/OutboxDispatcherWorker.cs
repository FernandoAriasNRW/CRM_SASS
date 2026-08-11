using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Outbox;

public class OutboxDispatcherWorker(
    IServiceProvider serviceProvider,
    ILogger<OutboxDispatcherWorker> logger) : BackgroundService
{
  private readonly IServiceProvider _serviceProvider = serviceProvider;
  private readonly ILogger<OutboxDispatcherWorker> _logger = logger;
  private readonly TimeSpan _interval = TimeSpan.FromSeconds(5);

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    _logger.LogInformation("Outbox Dispatcher Worker started");

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await ProcessOutboxMessagesAsync(stoppingToken);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error processing outbox messages");
      }

      await Task.Delay(_interval, stoppingToken);
    }
  }

  private async Task ProcessOutboxMessagesAsync(CancellationToken ct)
  {
    using var scope = _serviceProvider.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<CrmDbContext>();

    var messages = await context.OutboxMessages
        .Where(m => m.ProcessedAt == null)
        .OrderBy(m => m.CreatedAt)
        .Take(10)
        .ToListAsync(ct);

    foreach (var message in messages)
    {
      try
      {
        _logger.LogInformation(
            "Processing outbox message: {EventType} ({Id})",
            message.Type,
            message.Id);

        var publishEndpoint = scope.ServiceProvider.GetRequiredService<MassTransit.IPublishEndpoint>();

        // Intentar encontrar el tipo real del evento en todos los ensamblados cargados
        var eventType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(message.Type))
            .FirstOrDefault(t => t != null);

        if (eventType != null)
        {
            var eventObject = System.Text.Json.JsonSerializer.Deserialize(message.Payload, eventType);
            if (eventObject != null)
            {
                await publishEndpoint.Publish(eventObject, eventType, ct);
            }
        }
        else
        {
            _logger.LogWarning("Event type {EventType} not found. Publishing as raw JSON.", message.Type);
            // Fallback si no se encuentra el tipo, aunque idealmente debería encontrarse
            // Podemos envolverlo en un evento genérico si es necesario
        }

        message.ProcessedAt = DateTime.UtcNow;
        context.Update(message);
        await context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Outbox message processed successfully: {Id}",
            message.Id);
      }
      catch (Exception ex)
      {
        _logger.LogError(
            ex,
            "Failed to process outbox message: {Id}",
            message.Id);

        message.ProcessedAt = DateTime.UtcNow;
        context.Update(message);
        await context.SaveChangesAsync(ct);
      }
    }
  }
}