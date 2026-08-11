using BuildingBlocks.Application.Abstractions;
using Webhook.Application.DTOs;

namespace Webhook.Application.Commands;

public sealed record ToggleWebhookSubscriptionCommand(
    Guid TenantId,
    Guid SubscriptionId,
    bool Activate
) : ICommand<WebhookSubscriptionDto>;

