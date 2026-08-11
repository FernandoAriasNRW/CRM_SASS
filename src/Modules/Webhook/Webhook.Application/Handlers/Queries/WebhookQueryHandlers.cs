using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Webhook.Application.Abstractions.Repositories;
using Webhook.Application.DTOs;
using Webhook.Application.Queries;

namespace Webhook.Application.Handlers.Queries;

public sealed class GetWebhookSubscriptionsHandler(IWebhookSubscriptionRepository repository)
    : IQueryHandler<GetWebhookSubscriptionsQuery, IReadOnlyList<WebhookSubscriptionDto>>
{
    public async Task<Result<IReadOnlyList<WebhookSubscriptionDto>>> Handle(
        GetWebhookSubscriptionsQuery request, CancellationToken ct)
    {
        var items = await repository.GetByTenantAsync(request.TenantId, request.EventName, ct);
        return Result<IReadOnlyList<WebhookSubscriptionDto>>.Success(
            items.Select(WebhookSubscriptionDto.FromEntity).ToList());
    }
}

public sealed class GetWebhookSubscriptionByIdHandler(IWebhookSubscriptionRepository repository)
    : IQueryHandler<GetWebhookSubscriptionByIdQuery, WebhookSubscriptionDto?>
{
    public async Task<Result<WebhookSubscriptionDto?>> Handle(
        GetWebhookSubscriptionByIdQuery request, CancellationToken ct)
    {
        var sub = await repository.GetByIdAsync(request.TenantId, request.SubscriptionId, ct);
        return Result<WebhookSubscriptionDto?>.Success(sub is null ? null : WebhookSubscriptionDto.FromEntity(sub));
    }
}
