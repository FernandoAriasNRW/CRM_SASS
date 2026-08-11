using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Reporting.Application.Dashboards.Commands;
using Reporting.Application.Dashboards.Queries;

namespace Reporting.Presentation.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/dashboards").WithTags("Dashboards").RequireAuthorization();

        group.MapGet("", async (System.Security.Claims.ClaimsPrincipal principal, IMediator mediator) =>
        {
            var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
            var userId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;
            
            var query = new GetDashboardsQuery(tenantId, userId);
            var result = await mediator.Send(query);
            return Results.Ok(result);
        });

        group.MapPost("", async (System.Security.Claims.ClaimsPrincipal principal, CreateDashboardRequest request, IMediator mediator) =>
        {
            var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
            var userId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;

            var command = new CreateDashboardCommand(
                tenantId,
                request.Title,
                request.IsDefault,
                request.IsPublic,
                userId,
                request.WidgetsJson,
                request.TagIds
            );

            var result = await mediator.Send(command);
            return Results.Created($"/api/v1/dashboards/{result}", result);
        });

        group.MapPut("/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, CreateDashboardRequest request, IMediator mediator) =>
        {
            var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
            var userId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;

            var command = new UpdateDashboardCommand(
                tenantId,
                id,
                userId,
                request.Title,
                request.IsDefault,
                request.IsPublic,
                request.WidgetsJson,
                request.TagIds
            );

            var result = await mediator.Send(command);
            return result ? Results.Ok() : Results.Forbid();
        });

        group.MapDelete("/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator) =>
        {
            var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
            var userId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;

            var command = new DeleteDashboardCommand(tenantId, id, userId);
            var result = await mediator.Send(command);
            return result ? Results.Ok() : Results.Forbid();
        });

        return app;
    }
}

public record CreateDashboardRequest(
    string Title,
    bool IsDefault,
    bool IsPublic,
    string WidgetsJson,
    List<Guid> TagIds);
