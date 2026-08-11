using BuildingBlocks.Domain.Primitives;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// Servicio de Outbox que persiste mensajes de eventos de dominio.
/// Usa CrmDbContext centralizado para mantener todos los mensajes de outbox.
/// Esto asegura que todos los módulos compartan la misma tabla de outbox.
/// </summary>
public sealed class OutboxService(IServiceProvider serviceProvider, ILogger<OutboxService> logger) : IOutboxService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<OutboxService> _logger = logger;

    public async Task AddMessageAsync(string eventType, string payload, CancellationToken ct = default)
    {
        try
        {
            // Crear un scope para obtener el CrmDbContext central
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CrmDbContext>();

            var outboxMessage = new OutboxMessageEntity
            {
                Id = Guid.NewGuid(),
                Type = eventType,
                Payload = payload,
                CreatedAt = DateTime.UtcNow
            };

            context.OutboxMessages.Add(outboxMessage);
            await context.SaveChangesAsync(ct);

            _logger.LogDebug("Outbox message added: {EventType} - {MessageId}", eventType, outboxMessage.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add outbox message: {EventType}", eventType);
            throw;
        }
    }
}