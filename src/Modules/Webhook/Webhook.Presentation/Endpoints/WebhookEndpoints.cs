using BuildingBlocks.Application.Abstractions;
using System.Linq;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Webhook.Application.Commands;
using Webhook.Application.Queries;
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

    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/webhooks")
            .WithTags("Webhooks")
            .RequireAuthorization();

        // GET /api/v1/webhooks?tenantId=&eventName=
        group.MapGet("", async (IUserContext currentUser, string? eventName, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
            var result = await mediator.Send(new GetWebhookSubscriptionsQuery(tenantId, eventName));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        // GET /api/v1/webhooks/{id}
        group.MapGet("/{id:guid}", async (IUserContext currentUser, Guid id, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
            var result = await mediator.Send(new GetWebhookSubscriptionByIdQuery(tenantId, id));
            return result.Value is null ? Results.NotFound() : Results.Ok(result.Value);
        });

        // POST /api/v1/webhooks — crear suscripción
        // Inquilino del token. Ver ProjectsEndpoints: misma grieta, y aquí más cara —una
        // suscripción plantada en otra organización recibiría sus eventos en una URL ajena.
        group.MapPost("", async (CreateWebhookCommand command, IUserContext usuario, IMediator mediator) =>
        {
            var result = await mediator.Send(command with { TenantId = usuario.TenantId });
            return result.IsSuccess
                ? Results.Created($"/api/v1/webhooks/{result.Value!.Id}", result.Value)
                : Results.BadRequest(result.Error);
        });

        // PATCH /api/v1/webhooks/{id} — actualizar URL/secret
        group.MapPatch("/{id:guid}", async (IUserContext currentUser, Guid id, UpdateWebhookSubscriptionCommand body, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
            var result = await mediator.Send(new UpdateWebhookSubscriptionCommand(tenantId, id, body.TargetUrl, body.Secret));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // DELETE /api/v1/webhooks/{id} — eliminar suscripción
        group.MapDelete("/{id:guid}", async (IUserContext currentUser, Guid id, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
            var result = await mediator.Send(new DeleteWebhookSubscriptionCommand(tenantId, id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        });

        // PATCH /api/v1/webhooks/{id}/toggle — activar/desactivar
        group.MapPatch("/{id:guid}/toggle", async (IUserContext currentUser, Guid id, bool activate, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
            var result = await mediator.Send(new ToggleWebhookSubscriptionCommand(tenantId, id, activate));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // POST /api/v1/webhooks/dispatch — dispatch interno (solo admin / internal calls)
        // Aunque exija ser administrador, un administrador lo es *de su organización*: sin esto
        // podría lanzar eventos en el inquilino de otra.
        group.MapPost("/dispatch", async (DispatchWebhookEventCommand command, IUserContext usuario, IMediator mediator) =>
        {
            var result = await mediator.Send(command with { TenantId = usuario.TenantId });
            return result.IsSuccess ? Results.Accepted() : Results.BadRequest(result.Error);
        }).RequireAuthorization("AdminOnly");

        return app;
    }
}
