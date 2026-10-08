using System.Security.Cryptography;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Webhook.Application.Abstractions;
using Webhook.Application.Abstractions.Repositories;
using Webhook.Domain;
using Webhook.Domain.Entities;

namespace Webhook.Application.Handlers;

/// <summary>Los secretos de firma: 32 bytes aleatorios, con un prefijo que dice qué son si aparecen sueltos.</summary>
internal static class WebhookSecrets
{
    public static string New() => "whsec_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
}

public sealed class CreateWebhookHandler(
    TimeProvider timeProvider,
    IWebhookSubscriptionRepository subscriptions,
    IWebhookUrlPolicy urlPolicy,
    IWebhookUnitOfWork unitOfWork) : ICommandHandler<CreateWebhookCommand, WebhookWithSecretDto>
{
    public async Task<Result<WebhookWithSecretDto>> Handle(CreateWebhookCommand request, CancellationToken ct)
    {
        if (urlPolicy.Reject(request.Url) is { } rejected)
            return Result<WebhookWithSecretDto>.Failure(rejected);

        WebhookSubscription subscription;
        try
        {
            subscription = WebhookSubscription.Create(
                timeProvider.GetUtcNow().UtcDateTime, request.TenantId, request.Name, request.Url,
                request.EventTypes ?? [], WebhookSecrets.New());
        }
        catch (InvalidOperationException ex) { return Result<WebhookWithSecretDto>.Failure(ex.Message); }

        await subscriptions.AddAsync(subscription, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<WebhookWithSecretDto>.Success(new(WebhookSubscriptionDto.From(subscription, null), subscription.Secret));
    }
}

public sealed class UpdateWebhookHandler(
    TimeProvider timeProvider,
    IWebhookSubscriptionRepository subscriptions,
    IWebhookDeliveryRepository deliveries,
    IWebhookUrlPolicy urlPolicy,
    IWebhookUnitOfWork unitOfWork) : ICommandHandler<UpdateWebhookCommand, WebhookSubscriptionDto>
{
    public async Task<Result<WebhookSubscriptionDto>> Handle(UpdateWebhookCommand request, CancellationToken ct)
    {
        var subscription = await subscriptions.GetByIdAsync(request.TenantId, request.SubscriptionId, ct);
        if (subscription is null) return Result<WebhookSubscriptionDto>.Failure(WebhookSubscription.Rules.NotFound);

        if (urlPolicy.Reject(request.Url) is { } rejected)
            return Result<WebhookSubscriptionDto>.Failure(rejected);

        try
        {
            subscription.Update(timeProvider.GetUtcNow().UtcDateTime, request.Name, request.Url, request.EventTypes ?? [], request.IsActive);
        }
        catch (InvalidOperationException ex) { return Result<WebhookSubscriptionDto>.Failure(ex.Message); }

        await unitOfWork.SaveChangesAsync(ct);

        var stats = await deliveries.GetStatsAsync(request.TenantId, ct);
        return Result<WebhookSubscriptionDto>.Success(WebhookSubscriptionDto.From(subscription, stats.GetValueOrDefault(subscription.Id)));
    }
}

public sealed class DeleteWebhookHandler(
    IWebhookSubscriptionRepository subscriptions,
    IWebhookUnitOfWork unitOfWork) : ICommandHandler<DeleteWebhookCommand, bool>
{
    public async Task<Result<bool>> Handle(DeleteWebhookCommand request, CancellationToken ct)
    {
        var subscription = await subscriptions.GetByIdAsync(request.TenantId, request.SubscriptionId, ct);
        if (subscription is null) return Result<bool>.Failure(WebhookSubscription.Rules.NotFound);

        // Sus envíos pendientes se abandonan solos: el trabajo de entrega ya no encuentra la
        // suscripción y los da por fallidos sin llamar a nadie.
        subscriptions.Remove(subscription);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public sealed class RegenerateWebhookSecretHandler(
    TimeProvider timeProvider,
    IWebhookSubscriptionRepository subscriptions,
    IWebhookDeliveryRepository deliveries,
    IWebhookUnitOfWork unitOfWork) : ICommandHandler<RegenerateWebhookSecretCommand, WebhookWithSecretDto>
{
    public async Task<Result<WebhookWithSecretDto>> Handle(RegenerateWebhookSecretCommand request, CancellationToken ct)
    {
        var subscription = await subscriptions.GetByIdAsync(request.TenantId, request.SubscriptionId, ct);
        if (subscription is null) return Result<WebhookWithSecretDto>.Failure(WebhookSubscription.Rules.NotFound);

        subscription.RegenerateSecret(timeProvider.GetUtcNow().UtcDateTime, WebhookSecrets.New());
        await unitOfWork.SaveChangesAsync(ct);

        var stats = await deliveries.GetStatsAsync(request.TenantId, ct);
        return Result<WebhookWithSecretDto>.Success(new(
            WebhookSubscriptionDto.From(subscription, stats.GetValueOrDefault(subscription.Id)), subscription.Secret));
    }
}

public sealed class SendWebhookTestHandler(
    TimeProvider timeProvider,
    IWebhookSubscriptionRepository subscriptions,
    IWebhookDeliveryRepository deliveries,
    IWebhookDeliverySignal signal,
    IWebhookUnitOfWork unitOfWork) : ICommandHandler<SendWebhookTestCommand, WebhookDeliveryDto>
{
    public async Task<Result<WebhookDeliveryDto>> Handle(SendWebhookTestCommand request, CancellationToken ct)
    {
        var subscription = await subscriptions.GetByIdAsync(request.TenantId, request.SubscriptionId, ct);
        if (subscription is null) return Result<WebhookDeliveryDto>.Failure(WebhookSubscription.Rules.NotFound);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var delivery = WebhookDelivery.Create(now, request.TenantId, subscription.Id, WebhookEventCatalog.Test,
            id => WebhookPayload.Build(id, WebhookEventCatalog.Test, request.TenantId, now,
                new { message = "Prueba enviada desde la administración" }, null));

        await deliveries.AddAsync(delivery, ct);
        await unitOfWork.SaveChangesAsync(ct);
        signal.Notify();

        return Result<WebhookDeliveryDto>.Success(WebhookDeliveryDto.From(delivery));
    }
}

public sealed class GetWebhooksHandler(
    IWebhookSubscriptionRepository subscriptions,
    IWebhookDeliveryRepository deliveries) : IQueryHandler<GetWebhooksQuery, IReadOnlyList<WebhookSubscriptionDto>>
{
    public async Task<Result<IReadOnlyList<WebhookSubscriptionDto>>> Handle(GetWebhooksQuery request, CancellationToken ct)
    {
        var all = await subscriptions.GetByTenantAsync(request.TenantId, ct);
        var stats = await deliveries.GetStatsAsync(request.TenantId, ct);

        IReadOnlyList<WebhookSubscriptionDto> result = all
            .Select(s => WebhookSubscriptionDto.From(s, stats.GetValueOrDefault(s.Id)))
            .ToList();
        return Result<IReadOnlyList<WebhookSubscriptionDto>>.Success(result);
    }
}

public sealed class GetWebhookHandler(
    IWebhookSubscriptionRepository subscriptions,
    IWebhookDeliveryRepository deliveries) : IQueryHandler<GetWebhookQuery, WebhookSubscriptionDto>
{
    public async Task<Result<WebhookSubscriptionDto>> Handle(GetWebhookQuery request, CancellationToken ct)
    {
        var subscription = await subscriptions.GetByIdAsync(request.TenantId, request.SubscriptionId, ct);
        if (subscription is null) return Result<WebhookSubscriptionDto>.Failure(WebhookSubscription.Rules.NotFound);

        var stats = await deliveries.GetStatsAsync(request.TenantId, ct);
        return Result<WebhookSubscriptionDto>.Success(WebhookSubscriptionDto.From(subscription, stats.GetValueOrDefault(subscription.Id)));
    }
}

public sealed class GetWebhookSecretHandler(IWebhookSubscriptionRepository subscriptions)
    : IQueryHandler<GetWebhookSecretQuery, WebhookSecretDto>
{
    public async Task<Result<WebhookSecretDto>> Handle(GetWebhookSecretQuery request, CancellationToken ct)
    {
        var subscription = await subscriptions.GetByIdAsync(request.TenantId, request.SubscriptionId, ct);
        return subscription is null
            ? Result<WebhookSecretDto>.Failure(WebhookSubscription.Rules.NotFound)
            : Result<WebhookSecretDto>.Success(new(subscription.Secret));
    }
}

public sealed class GetWebhookDeliveriesHandler(
    IWebhookSubscriptionRepository subscriptions,
    IWebhookDeliveryRepository deliveries) : IQueryHandler<GetWebhookDeliveriesQuery, IReadOnlyList<WebhookDeliveryDto>>
{
    private const int Max = 50;

    public async Task<Result<IReadOnlyList<WebhookDeliveryDto>>> Handle(GetWebhookDeliveriesQuery request, CancellationToken ct)
    {
        if (await subscriptions.GetByIdAsync(request.TenantId, request.SubscriptionId, ct) is null)
            return Result<IReadOnlyList<WebhookDeliveryDto>>.Failure(WebhookSubscription.Rules.NotFound);

        IReadOnlyList<WebhookDeliveryDto> result = (await deliveries.GetRecentAsync(request.TenantId, request.SubscriptionId, Max, ct))
            .Select(WebhookDeliveryDto.From)
            .ToList();
        return Result<IReadOnlyList<WebhookDeliveryDto>>.Success(result);
    }
}
