using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Teams.Infrastructure;
using MediatR;
using Teams.Application.Commands;
using Teams.Application.Queries;

namespace Teams.Presentation.Endpoints;

public static class TeamsEndpoints
{
    public static IServiceCollection AddTeamsPresentation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTeamsInfrastructure(configuration);
        return services;
    }

    public static IEndpointRouteBuilder MapTeamsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/teams").WithTags("Teams").RequireAuthorization();

        group.MapGet("/", async (System.Security.Claims.ClaimsPrincipal principal, IMediator mediator) =>
        {
            var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
            var result = await mediator.Send(new GetTeamsQuery(tenantId));
            return Results.Ok(result.Value);
        });

        group.MapGet("/my-teams", async (System.Security.Claims.ClaimsPrincipal principal, IMediator mediator) =>
        {
            var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
            var userId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;
            var result = await mediator.Send(new GetMyTeamsQuery(tenantId, userId));
            return Results.Ok(result.Value);
        });

        group.MapGet("/{id:guid}", async (Guid id, System.Security.Claims.ClaimsPrincipal principal, IMediator mediator) =>
        {
            var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
            var result = await mediator.Send(new GetTeamByIdQuery(tenantId, id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        group.MapPost("/", async (CreateTeamRequest req, System.Security.Claims.ClaimsPrincipal principal, IMediator mediator) =>
        {
            var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
            var command = new CreateTeamCommand(tenantId, req.Name, req.Description, req.MemberIds);
            var result = await mediator.Send(command);
            return result.IsSuccess ? Results.Created($"/api/v1/teams/{result.Value}", result.Value) : Results.BadRequest(result.Error);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateTeamRequest req, System.Security.Claims.ClaimsPrincipal principal, IMediator mediator) =>
        {
            var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
            var command = new UpdateTeamCommand(tenantId, id, req.Name, req.Description, req.MemberIds);
            var result = await mediator.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        });

        group.MapDelete("/{id:guid}", async (Guid id, System.Security.Claims.ClaimsPrincipal principal, IMediator mediator) =>
        {
            var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
            var result = await mediator.Send(new DeleteTeamCommand(tenantId, id));
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        });

        return app;
    }
}

public record CreateTeamRequest(string Name, string Description, List<Guid> MemberIds);
public record UpdateTeamRequest(string Name, string Description, List<Guid> MemberIds);
