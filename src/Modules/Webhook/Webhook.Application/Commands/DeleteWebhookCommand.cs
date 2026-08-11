using BuildingBlocks.Application.Abstractions;

namespace Webhook.Application.Commands;

public sealed record DeleteWebhookSubscriptionCommand(
    Guid TenantId,
    Guid SubscriptionId
) : ICommand<bool>;
