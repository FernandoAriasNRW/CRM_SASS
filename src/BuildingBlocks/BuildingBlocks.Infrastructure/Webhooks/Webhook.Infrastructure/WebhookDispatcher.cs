using System.Net.Http.Json;
using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Infrastructure.Webhooks.Webhook.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Webhooks.Webhook.Infrastructure;

public sealed class WebhookDispatcher : IWebhookDispatcher
{
  private readonly CrmDbContext _context;
  private readonly HttpClient _httpClient;
  private readonly ILogger<WebhookDispatcher> _logger;

  public WebhookDispatcher(CrmDbContext context, HttpClient httpClient, ILogger<WebhookDispatcher> logger)
  {
    _context = context;
    _httpClient = httpClient;
    _logger = logger;
  }

  public async Task ProcessDeliveriesAsync(int batchSize, CancellationToken ct)
  {
    // 1. Obtener mensajes pendientes (Outbox Pattern)
    var pendingMessages = await _context.OutboxMessages
        .Where(m => m.ProcessedAt == null && m.Type.Contains("Webhook"))
        .OrderBy(m => m.CreatedAt)
        .Take(batchSize)
        .ToListAsync(ct);

    if (!pendingMessages.Any()) return;

    foreach (var message in pendingMessages)
    {
      try
      {
        // Aquí podrías deserializar el Payload si necesitas una URL específica por ahora simulamos un POST genérico
        var response = await _httpClient.PostAsJsonAsync("URL_DESTINO", message.Payload, ct);

        if (response.IsSuccessStatusCode)
        {
          message.ProcessedAt = DateTime.UtcNow;
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error enviando Webhook {MessageId}", message.Id);
      }
    }

    await _context.SaveChangesAsync(ct);
  }
}