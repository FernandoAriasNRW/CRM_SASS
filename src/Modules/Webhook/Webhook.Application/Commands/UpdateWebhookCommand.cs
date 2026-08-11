using BuildingBlocks.Application.Abstractions;
using Webhook.Application.DTOs;

namespace Webhook.Application.Commands;

public sealed record UpdateWebhookSubscriptionCommand(
    Guid TenantId,
    Guid SubscriptionId,
    string? TargetUrl,
    string? Secret
) : ICommand<WebhookSubscriptionDto>;
