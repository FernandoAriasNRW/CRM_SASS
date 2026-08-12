using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Webhook.Application.Abstractions;
using Webhook.Application.Abstractions.Repositories;
using Webhook.Application.Commands;
using Webhook.Application.DTOs;
using Webhook.Domain.Entities;

namespace Webhook.Application.Handlers.Commands;

public sealed class CreateWebhookSubscriptionHandler(
    IWebhookSubscriptionRepository repository,
    IWebhookUnitOfWork unitOfWork) : ICommandHandler<CreateWebhookCommand, WebhookSubscriptionDto>
{
    public async Task<Result<WebhookSubscriptionDto>> Handle(CreateWebhookCommand request, CancellationToken ct)
    {
        var subscription = WebhookSubscription.Create(
            request.TenantId, request.EventName, request.TargetUrl, request.Secret);

        await repository.AddAsync(subscription, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<WebhookSubscriptionDto>.Success(WebhookSubscriptionDto.FromEntity(subscription));
    }
}

public sealed class UpdateWebhookSubscriptionHandler(
    IWebhookSubscriptionRepository repository,
    IWebhookUnitOfWork unitOfWork) : ICommandHandler<UpdateWebhookSubscriptionCommand, WebhookSubscriptionDto>
{
    public async Task<Result<WebhookSubscriptionDto>> Handle(UpdateWebhookSubscriptionCommand request, CancellationToken ct)
    {
        var sub = await repository.GetByIdAsync(request.TenantId, request.SubscriptionId, ct);
        if (sub is null)
            return Result<WebhookSubscriptionDto>.Failure("Subscription not found");

        sub.Update(request.TargetUrl, request.Secret);
        await repository.UpdateAsync(sub, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<WebhookSubscriptionDto>.Success(WebhookSubscriptionDto.FromEntity(sub));
    }
}

public sealed class DeleteWebhookSubscriptionHandler(
    IWebhookSubscriptionRepository repository,
    IWebhookUnitOfWork unitOfWork) : ICommandHandler<DeleteWebhookSubscriptionCommand, bool>
{
    public async Task<Result<bool>> Handle(DeleteWebhookSubscriptionCommand request, CancellationToken ct)
    {
        var sub = await repository.GetByIdAsync(request.TenantId, request.SubscriptionId, ct);
        if (sub is null)
            return Result<bool>.Failure("Subscription not found");

        await repository.DeleteAsync(sub, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}

public sealed class ToggleWebhookSubscriptionHandler(
    IWebhookSubscriptionRepository repository,
    IWebhookUnitOfWork unitOfWork) : ICommandHandler<ToggleWebhookSubscriptionCommand, WebhookSubscriptionDto>
{
    public async Task<Result<WebhookSubscriptionDto>> Handle(ToggleWebhookSubscriptionCommand request, CancellationToken ct)
    {
        var sub = await repository.GetByIdAsync(request.TenantId, request.SubscriptionId, ct);
        if (sub is null)
            return Result<WebhookSubscriptionDto>.Failure("Subscription not found");

        if (request.Activate) sub.Activate(); else sub.Deactivate();
        await repository.UpdateAsync(sub, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<WebhookSubscriptionDto>.Success(WebhookSubscriptionDto.FromEntity(sub));
    }
}

public sealed class DispatchWebhookEventHandler(
    IWebhookDispatchService dispatchService) : ICommandHandler<DispatchWebhookEventCommand, bool>
{
    public async Task<Result<bool>> Handle(DispatchWebhookEventCommand request, CancellationToken ct)
    {
        await dispatchService.DispatchAsync(request.EventName, request.TenantId, request.EventData, ct);
        return Result<bool>.Success(true);
    }
}
