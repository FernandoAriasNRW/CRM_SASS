using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Intercepta commands marcados con IWebhookTriggered.
/// Tras un resultado exitoso publica WebhookEventNotification que
/// el módulo Webhook escucha para despachar a los suscriptores del tenant.
/// </summary>
public sealed class WebhookDispatchBehavior<TRequest, TResponse>(
    IPublisher publisher,
    ILogger<WebhookDispatchBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IWebhookTriggered
    where TResponse : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var response = await next();

        var isSuccess = response switch
        {
            Result r       => r.IsSuccess,
            Result<bool> r => r.IsSuccess,
            _              => true
        };

        if (!isSuccess) return response;

        try
        {
            await publisher.Publish(
                new WebhookEventNotification(request.WebhookEventName, request.TenantId, request),
                ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Webhook dispatch failed for event {Event}", request.WebhookEventName);
        }

        return response;
    }
}

/// <summary>
/// Notificación MediatR desacoplada — BuildingBlocks no depende de Webhook.Application.
/// El handler vive en Webhook.Application y llama a IWebhookDispatchService.
/// </summary>
public sealed record WebhookEventNotification(
    string EventName,
    Guid TenantId,
    object EventData) : INotification;
