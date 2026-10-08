using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Webhook.Domain.Entities;
using Webhook.Infrastructure.Persistence;

namespace Webhook.Infrastructure.Delivery;

/// <summary>
/// Manda los envíos pendientes y reintenta los que fallaron.
///
/// Cada envío lleva estas cabeceras:
/// <list type="bullet">
/// <item><c>X-Webhook-Event</c>: el nombre del evento.</item>
/// <item><c>X-Webhook-Delivery</c>: el identificador del envío, el mismo en todos sus reintentos.</item>
/// <item><c>X-Webhook-Timestamp</c>: segundos desde 1970 en que se firmó este intento.</item>
/// <item><c>X-Webhook-Signature</c>: <c>sha256=</c> y el HMAC-SHA256 en hexadecimal de
/// <c>{timestamp}.{cuerpo}</c> con el secreto de la suscripción. Firmar también la hora permite al
/// receptor rechazar un envío viejo que alguien le reenvíe.</item>
/// </list>
///
/// Una respuesta 2xx es un éxito; cualquier otra cosa, o no responder a tiempo, es un fallo que se
/// reintenta según <see cref="WebhookDelivery.RetryDelays"/>.
/// </summary>
internal sealed class WebhookDeliveryWorker(
    IServiceProvider serviceProvider,
    IHttpClientFactory httpClientFactory,
    WebhookDeliverySignal signal,
    TimeProvider timeProvider,
    IOptions<WebhookOptions> options,
    ILogger<WebhookDeliveryWorker> logger) : BackgroundService
{
    /// <summary>Cuántos envíos se mandan por vuelta. Lo que no quepa sale en la siguiente.</summary>
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Si la tanda salió llena, puede quedar más: se sigue sin esperar.
                while (await DeliverDueAsync(stoppingToken) == BatchSize) { }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Un fallo no puede tumbar el trabajo: si el bucle muere, no sale ni un webhook más
                // y nada lo dice.
                logger.LogError(ex, "Error entregando webhooks");
            }

            try { await signal.WaitAsync(interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task<int> DeliverDueAsync(CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WebhookDbContext>();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Sin filtro de inquilino, a propósito y sólo aquí: el trabajo reparte los envíos de todas
        // las organizaciones. No hay borrado lógico ni archivo en estas tablas, así que quitar el
        // filtro no deja pasar nada escondido. Lo que se haga con cada envío ya va con su inquilino.
        var due = await db.Deliveries.IgnoreQueryFilters()
            .Where(d => d.Status == WebhookDeliveryStatus.Pending && d.NextAttemptAtUtc <= now)
            .OrderBy(d => d.NextAttemptAtUtc)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var delivery in due)
        {
            var subscription = await db.Subscriptions.IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Id == delivery.SubscriptionId && s.TenantId == delivery.TenantId, ct);

            if (subscription is null || !subscription.IsActive)
            {
                delivery.Abandon(timeProvider.GetUtcNow().UtcDateTime,
                    subscription is null ? "La suscripción ya no existe" : "La suscripción está desactivada");
                continue;
            }

            await SendAsync(delivery, subscription, ct);
        }

        await db.SaveChangesAsync(ct);
        return due.Count;
    }

    private async Task SendAsync(WebhookDelivery delivery, WebhookSubscription subscription, CancellationToken ct)
    {
        var timestamp = timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString();

        using var request = new HttpRequestMessage(HttpMethod.Post, subscription.TargetUrl)
        {
            Content = new StringContent(delivery.Payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Webhook-Event", delivery.EventName);
        request.Headers.Add("X-Webhook-Delivery", delivery.Id.ToString());
        request.Headers.Add("X-Webhook-Timestamp", timestamp);
        request.Headers.Add("X-Webhook-Signature", "sha256=" + Sign(subscription.Secret, timestamp, delivery.Payload));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.Value.TimeoutSeconds)));

        var watch = Stopwatch.StartNew();
        try
        {
            using var response = await httpClientFactory.CreateClient(WebhookHttpClient.Name).SendAsync(request, timeout.Token);
            var code = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
                delivery.RecordSuccess(timeProvider.GetUtcNow().UtcDateTime, code);
            else
                delivery.RecordFailure(timeProvider.GetUtcNow().UtcDateTime, code, $"El destino respondió {code}");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            delivery.RecordFailure(timeProvider.GetUtcNow().UtcDateTime, null,
                $"El destino no respondió en {options.Value.TimeoutSeconds} segundos");
        }
        catch (HttpRequestException ex)
        {
            delivery.RecordFailure(timeProvider.GetUtcNow().UtcDateTime, null, ex.Message);
        }

        logger.LogInformation("Webhook {Event} a {Subscription}: {Status} en {Elapsed} ms (intento {Attempt})",
            delivery.EventName, subscription.Id, delivery.Status, watch.ElapsedMilliseconds, delivery.Attempts);
    }

    /// <summary>HMAC-SHA256 de <c>{timestamp}.{cuerpo}</c>, en hexadecimal y minúsculas.</summary>
    public static string Sign(string secret, string timestamp, string payload)
        => Convert.ToHexString(HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(secret),
                Encoding.UTF8.GetBytes($"{timestamp}.{payload}")))
            .ToLowerInvariant();
}
