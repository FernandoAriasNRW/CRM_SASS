using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Intercepta los comandos marcados con <see cref="IWebhookTriggered"/> y, si salen bien, publica
/// una <see cref="WebhookEventNotification"/> que el módulo Webhook convierte en envíos.
///
/// <b>«Salir bien» se mira en cualquier resultado.</b> Sólo se reconocían <c>Result</c> y
/// <c>Result&lt;bool&gt;</c>: un <c>Result&lt;TaskDto&gt;</c> fallido caía en el caso por defecto,
/// se daba por bueno y el webhook anunciaba una tarea que no se había creado. Ahora se lee
/// <c>IsSuccess</c> de cualquier <c>Result&lt;T&gt;</c>.
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

        var (isSuccess, value) = Outcome(response);
        if (!isSuccess) return response;

        try
        {
            await publisher.Publish(
                new WebhookEventNotification(request.WebhookEventName, request.TenantId, request, value),
                ct);
        }
        catch (Exception ex)
        {
            // Encolar un envío no puede tumbar la operación que ya se hizo.
            logger.LogWarning(ex, "Webhook dispatch failed for event {Event}", request.WebhookEventName);
        }

        return response;
    }

    /// <summary>Si la respuesta es un éxito y, si lleva un valor, cuál.</summary>
    private static (bool IsSuccess, object? Value) Outcome(TResponse response)
    {
        if (response is Result plain) return (plain.IsSuccess, null);

        var type = response.GetType();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var isSuccess = (bool)type.GetProperty(nameof(Result<object>.IsSuccess))!.GetValue(response)!;
            var value = type.GetProperty(nameof(Result<object>.Value))!.GetValue(response);
            return (isSuccess, value);
        }

        return (true, null);
    }
}

/// <summary>
/// Un comando que dispara webhooks salió bien. La escucha el módulo Webhook —BuildingBlocks no lo
/// conoce— y la convierte en envíos a las suscripciones que quieren este evento.
/// </summary>
/// <param name="Input">El comando tal cual. El módulo Webhook quita lo sensible antes de mandarlo.</param>
/// <param name="Result">El valor que devolvió, si devolvió alguno: el identificador de lo creado, por ejemplo.</param>
public sealed record WebhookEventNotification(
    string EventName,
    Guid TenantId,
    object Input,
    object? Result) : INotification;
