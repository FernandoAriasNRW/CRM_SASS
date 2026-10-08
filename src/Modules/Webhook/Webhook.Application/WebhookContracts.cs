using BuildingBlocks.Application.Abstractions;
using Webhook.Domain.Entities;

namespace Webhook.Application;

// ── Lo que ve la pantalla ────────────────────────────────────────────────────────────────────

/// <summary>
/// Una suscripción tal como la enseña la administración. No lleva el secreto: se pide aparte
/// (<see cref="GetWebhookSecretQuery"/>), para que no viaje en cada listado.
/// </summary>
public sealed record WebhookSubscriptionDto(
    Guid Id,
    string Name,
    string Url,
    IReadOnlyList<string> EventTypes,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    int SuccessCount,
    int FailureCount,
    int PendingCount,
    DateTime? LastDeliveryAtUtc)
{
    public static WebhookSubscriptionDto From(WebhookSubscription s, Abstractions.Repositories.WebhookDeliveryStats? stats) =>
        new(s.Id, s.Name, s.TargetUrl, s.EventTypes, s.IsActive, s.CreatedAt, s.UpdatedAt,
            stats?.Succeeded ?? 0, stats?.Failed ?? 0, stats?.Pending ?? 0, stats?.LastDeliveryAtUtc);
}

/// <summary>Lo que devuelve crear una suscripción o cambiar su secreto: la suscripción y el secreto.</summary>
public sealed record WebhookWithSecretDto(WebhookSubscriptionDto Subscription, string Secret);

public sealed record WebhookSecretDto(string Secret);

/// <summary>Un envío, con lo justo para saber si llegó y por qué no.</summary>
public sealed record WebhookDeliveryDto(
    Guid Id,
    string EventName,
    string Status,
    int Attempts,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? NextAttemptAtUtc,
    int? LastStatusCode,
    string? LastError)
{
    public static WebhookDeliveryDto From(WebhookDelivery d) =>
        new(d.Id, d.EventName, d.Status.ToString(), d.Attempts, d.CreatedAtUtc, d.CompletedAtUtc,
            d.Status == WebhookDeliveryStatus.Pending ? d.NextAttemptAtUtc : null,
            d.LastStatusCode, d.LastError);
}

public sealed record WebhookEventDto(string Name, string Category);

// ── Comandos ─────────────────────────────────────────────────────────────────────────────────

public sealed record CreateWebhookCommand(Guid TenantId, string Name, string Url, IReadOnlyList<string> EventTypes)
    : ICommand<WebhookWithSecretDto>;

public sealed record UpdateWebhookCommand(
    Guid TenantId, Guid SubscriptionId, string Name, string Url, IReadOnlyList<string> EventTypes, bool IsActive)
    : ICommand<WebhookSubscriptionDto>;

public sealed record DeleteWebhookCommand(Guid TenantId, Guid SubscriptionId) : ICommand<bool>;

public sealed record RegenerateWebhookSecretCommand(Guid TenantId, Guid SubscriptionId) : ICommand<WebhookWithSecretDto>;

/// <summary>Manda un evento de prueba a la suscripción, para comprobar que el destino responde.</summary>
public sealed record SendWebhookTestCommand(Guid TenantId, Guid SubscriptionId) : ICommand<WebhookDeliveryDto>;

// ── Consultas ────────────────────────────────────────────────────────────────────────────────

public sealed record GetWebhooksQuery(Guid TenantId) : IQuery<IReadOnlyList<WebhookSubscriptionDto>>;

public sealed record GetWebhookQuery(Guid TenantId, Guid SubscriptionId) : IQuery<WebhookSubscriptionDto>;

public sealed record GetWebhookSecretQuery(Guid TenantId, Guid SubscriptionId) : IQuery<WebhookSecretDto>;

public sealed record GetWebhookDeliveriesQuery(Guid TenantId, Guid SubscriptionId) : IQuery<IReadOnlyList<WebhookDeliveryDto>>;
