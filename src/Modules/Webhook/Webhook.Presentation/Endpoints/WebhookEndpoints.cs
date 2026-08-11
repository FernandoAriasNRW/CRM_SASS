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
        group.MapGet("", async (System.Security.Claims.ClaimsPrincipal principal, string? eventName, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
            var result = await mediator.Send(new GetWebhookSubscriptionsQuery(tenantId, eventName));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        // GET /api/v1/webhooks/{id}
        group.MapGet("/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
            var result = await mediator.Send(new GetWebhookSubscriptionByIdQuery(tenantId, id));
            return result.Value is null ? Results.NotFound() : Results.Ok(result.Value);
        });

        // POST /api/v1/webhooks — crear suscripción
        group.MapPost("", async (CreateWebhookCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/v1/webhooks/{result.Value!.Id}", result.Value)
                : Results.BadRequest(result.Error);
        });

        // PATCH /api/v1/webhooks/{id} — actualizar URL/secret
        group.MapPatch("/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, UpdateWebhookSubscriptionCommand body, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
            var result = await mediator.Send(new UpdateWebhookSubscriptionCommand(tenantId, id, body.TargetUrl, body.Secret));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // DELETE /api/v1/webhooks/{id} — eliminar suscripción
        group.MapDelete("/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
            var result = await mediator.Send(new DeleteWebhookSubscriptionCommand(tenantId, id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        });

        // PATCH /api/v1/webhooks/{id}/toggle — activar/desactivar
        group.MapPatch("/{id:guid}/toggle", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, bool activate, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
            var result = await mediator.Send(new ToggleWebhookSubscriptionCommand(tenantId, id, activate));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // POST /api/v1/webhooks/dispatch — dispatch interno (solo admin / internal calls)
        group.MapPost("/dispatch", async (DispatchWebhookEventCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.IsSuccess ? Results.Accepted() : Results.BadRequest(result.Error);
        }).RequireAuthorization("AdminOnly");

        return app;
    }
}
