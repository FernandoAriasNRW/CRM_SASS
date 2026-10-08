using BuildingBlocks.Domain.Primitives;

namespace Webhook.Domain.Entities;

/// <summary>
/// Un envío de un evento a una suscripción, con sus intentos.
///
/// <b>Se guarda antes de mandarse.</b> Antes el POST se hacía dentro de la petición que causó el
/// evento —crear una tarea esperaba a que respondiera el servidor de fuera— y un fallo se tragaba
/// sin dejar rastro: ni reintento, ni registro, ni forma de saber que el destino llevaba un mes
/// caído. Ahora el envío queda pendiente, lo manda un trabajo en segundo plano y se reintenta con
/// esperas crecientes; cada intento deja su resultado.
/// </summary>
public sealed class WebhookDelivery : Entity, ITenantEntity
{
    public const int ErrorMaxLength = 500;

    /// <summary>
    /// Cuánto se espera antes de cada reintento. Tras el último se da por fallido: un destino que
    /// lleva horas sin responder no va a mejorar por insistir, y seguir llenaría la cola.
    /// </summary>
    public static readonly IReadOnlyList<TimeSpan> RetryDelays =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(6),
    ];

    public static int MaxAttempts => RetryDelays.Count + 1;

    public Guid TenantId { get; private set; }
    public Guid SubscriptionId { get; private set; }
    public string EventName { get; private set; } = string.Empty;

    /// <summary>El cuerpo que se manda, ya serializado: el mismo en todos los intentos.</summary>
    public string Payload { get; private set; } = string.Empty;

    public WebhookDeliveryStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime NextAttemptAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public int? LastStatusCode { get; private set; }
    public string? LastError { get; private set; }

    private WebhookDelivery() { }

    /// <param name="payloadFor">
    /// El cuerpo, a partir del identificador del envío: va dentro, para que el suscriptor pueda
    /// descartar un reintento que ya había procesado.
    /// </param>
    public static WebhookDelivery Create(
        DateTime nowUtc, Guid tenantId, Guid subscriptionId, string eventName, Func<Guid, string> payloadFor)
    {
        var id = Guid.NewGuid();
        return new()
        {
            Id = id,
            TenantId = tenantId,
            SubscriptionId = subscriptionId,
            EventName = eventName,
            Payload = payloadFor(id),
            Status = WebhookDeliveryStatus.Pending,
            CreatedAtUtc = nowUtc,
            NextAttemptAtUtc = nowUtc,
        };
    }

    public void RecordSuccess(DateTime nowUtc, int statusCode)
    {
        Attempts++;
        Status = WebhookDeliveryStatus.Succeeded;
        LastStatusCode = statusCode;
        LastError = null;
        CompletedAtUtc = nowUtc;
    }

    /// <summary>Anota un intento fallido y decide si habrá otro o se da por perdido.</summary>
    public void RecordFailure(DateTime nowUtc, int? statusCode, string error)
    {
        Attempts++;
        LastStatusCode = statusCode;
        LastError = error.Length <= ErrorMaxLength ? error : error[..ErrorMaxLength];

        if (Attempts >= MaxAttempts)
        {
            Status = WebhookDeliveryStatus.Failed;
            CompletedAtUtc = nowUtc;
            return;
        }

        NextAttemptAtUtc = nowUtc + RetryDelays[Attempts - 1];
    }

    /// <summary>Se abandona sin más intentos: la suscripción ya no existe o se desactivó.</summary>
    public void Abandon(DateTime nowUtc, string reason)
    {
        Status = WebhookDeliveryStatus.Failed;
        LastError = reason;
        CompletedAtUtc = nowUtc;
    }
}

public enum WebhookDeliveryStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
}
