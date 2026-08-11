using BuildingBlocks.Application.Abstractions;
using Webhook.Application.DTOs;
namespace Webhook.Application.Commands;

public record CreateWebhookCommand(
    string TargetUrl,
    string EventName,
    Guid TenantId,
    string Secret
) : ICommand<WebhookSubscriptionDto>;
