using BuildingBlocks.Application.Abstractions;
using Webhook.Application.DTOs;

namespace Webhook.Application.Queries;

public sealed record GetWebhookSubscriptionsQuery(
    Guid TenantId,
    string? EventName = null
) : IQuery<IReadOnlyList<WebhookSubscriptionDto>>;

public sealed record GetWebhookSubscriptionByIdQuery(
    Guid TenantId,
    Guid SubscriptionId
) : IQuery<WebhookSubscriptionDto?>;
