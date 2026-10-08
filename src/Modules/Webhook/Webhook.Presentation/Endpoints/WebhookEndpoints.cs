using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Webhook.Application;
using Webhook.Domain;
using Webhook.Domain.Entities;
using Webhook.Infrastructure;

namespace Webhook.Presentation.Endpoints;

public static class WebhookEndpoints
{
    public static IServiceCollection AddWebhookPresentation(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddWebhookInfrastructure(configuration);
        return services;
    }

    /// <summary>El cuerpo de crear o cambiar una suscripción.</summary>
    public sealed record WebhookRequest(string Name, string Url, IReadOnlyList<string>? EventTypes, bool IsActive = true);

    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        // Sólo administración, el grupo entero. Antes bastaba con tener sesión: cualquiera podía
        // crear un webhook y recibir los datos de toda la organización en su servidor. Y había un
        // POST /dispatch que disparaba un evento inventado hacia todos los suscriptores —pedía una
        // política «AdminOnly» que no existía, así que daba error siempre—; ya no está.
        var group = app.MapGroup("/api/v1/webhooks")
            .WithTags("Webhooks")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        // El catálogo: los eventos a los que se puede suscribir, con su grupo. Va antes que la ruta
        // con identificador para que «events» no compita con ella.
        group.MapGet("/events", () => Results.Ok(
            WebhookEventCatalog.All.Select(e => new WebhookEventDto(e.Name, e.Category))));

        group.MapGet("", async (IUserContext currentUser, IMediator mediator) =>
            ToResult(await mediator.Send(new GetWebhooksQuery(currentUser.TenantId))));

        group.MapGet("/{id:guid}", async (Guid id, IUserContext currentUser, IMediator mediator) =>
            ToResult(await mediator.Send(new GetWebhookQuery(currentUser.TenantId, id))));

        group.MapPost("", async (WebhookRequest body, IUserContext currentUser, IMediator mediator) =>
        {
            var result = await mediator.Send(new CreateWebhookCommand(currentUser.TenantId, body.Name, body.Url, body.EventTypes ?? []));
            return result.IsSuccess
                ? Results.Created($"/api/v1/webhooks/{result.Value!.Subscription.Id}", result.Value)
                : Results.BadRequest(result.Error);
        });

        group.MapPut("/{id:guid}", async (Guid id, WebhookRequest body, IUserContext currentUser, IMediator mediator) =>
            ToResult(await mediator.Send(new UpdateWebhookCommand(
                currentUser.TenantId, id, body.Name, body.Url, body.EventTypes ?? [], body.IsActive))));

        group.MapDelete("/{id:guid}", async (Guid id, IUserContext currentUser, IMediator mediator) =>
        {
            var result = await mediator.Send(new DeleteWebhookCommand(currentUser.TenantId, id));
            return result.IsSuccess ? Results.NoContent() : ToResult(result);
        });

        // El secreto se pide aparte para que no viaje en cada listado.
        group.MapGet("/{id:guid}/secret", async (Guid id, IUserContext currentUser, IMediator mediator) =>
            ToResult(await mediator.Send(new GetWebhookSecretQuery(currentUser.TenantId, id))));

        group.MapPost("/{id:guid}/regenerate-secret", async (Guid id, IUserContext currentUser, IMediator mediator) =>
            ToResult(await mediator.Send(new RegenerateWebhookSecretCommand(currentUser.TenantId, id))));

        group.MapPost("/{id:guid}/test", async (Guid id, IUserContext currentUser, IMediator mediator) =>
            ToResult(await mediator.Send(new SendWebhookTestCommand(currentUser.TenantId, id))));

        group.MapGet("/{id:guid}/deliveries", async (Guid id, IUserContext currentUser, IMediator mediator) =>
            ToResult(await mediator.Send(new GetWebhookDeliveriesQuery(currentUser.TenantId, id))));

        return app;
    }

    /// <summary>Un webhook que no es de la organización es 404; el resto de fallos, 400 con el motivo.</summary>
    private static IResult ToResult<T>(Result<T> result)
        => result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error == WebhookSubscription.Rules.NotFound ? Results.NotFound(result.Error) : Results.BadRequest(result.Error);
}
