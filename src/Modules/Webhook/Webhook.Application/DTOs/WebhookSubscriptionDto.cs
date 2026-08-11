using Webhook.Domain.Entities;

namespace Webhook.Application.DTOs;

public sealed record WebhookSubscriptionDto(
    Guid Id,
    Guid TenantId,
    string EventName,
    string TargetUrl,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static WebhookSubscriptionDto FromEntity(WebhookSubscription s) =>
        new(s.Id, s.TenantId, s.EventName, s.TargetUrl, s.IsActive, s.CreatedAt, s.UpdatedAt);
}

/// <summary>
/// Payload que se envía al suscriptor cuando se dispara un evento.
/// Contiene el nombre del evento, el body completo del comando que lo generó y metadatos.
/// </summary>
public sealed record WebhookPayload(
    string EventName,
    Guid TenantId,
    DateTime OccurredAt,
    object Data);
