using System.Linq;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ticketing.Application.Commands;
using Ticketing.Application.DTOs;
using Ticketing.Application.Queries;
using Ticketing.Infrastructure;

namespace Ticketing.Presentation.Endpoints;

public static class TicketingEndpoints
{
  public static IServiceCollection AddTicketingPresentation(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddTicketingInfrastructure(configuration);
    return services;
  }

  public static IEndpointRouteBuilder MapTicketingEndpoints(this IEndpointRouteBuilder app)
  {
    var group = app.MapGroup("/api/v1/tickets").WithTags("Tickets").RequireAuthorization();

    group.MapGet("", async (System.Security.Claims.ClaimsPrincipal principal, [Microsoft.AspNetCore.Mvc.FromQuery] Guid? customerId, [Microsoft.AspNetCore.Mvc.FromQuery] Guid? agentId, [Microsoft.AspNetCore.Mvc.FromQuery] string? priority, [Microsoft.AspNetCore.Mvc.FromQuery] string? status, IMediator mediator, [Microsoft.AspNetCore.Mvc.FromQuery] int page = 1, [Microsoft.AspNetCore.Mvc.FromQuery] int pageSize = 25, [Microsoft.AspNetCore.Mvc.FromQuery] string? sortColumn = null, [Microsoft.AspNetCore.Mvc.FromQuery] string? sortDirection = null, [Microsoft.AspNetCore.Mvc.FromQuery] DateTime? startDate = null, [Microsoft.AspNetCore.Mvc.FromQuery] DateTime? endDate = null) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var query = new GetTicketsQuery(tenantId, customerId, agentId, priority, status, new() { Page = page, PageSize = pageSize, SortColumn = sortColumn, SortDirection = sortDirection, StartDate = startDate, EndDate = endDate });
      var result = await mediator.Send(query);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var result = await mediator.Send(new GetTicketByIdQuery(tenantId, id));
      return result.Value is null ? Results.NotFound() : Results.Ok(result.Value);
    });

    group.MapPost("", async (System.Security.Claims.ClaimsPrincipal principal, CreateTicketCommand command, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var userId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value, out var _uid) ? _uid : Guid.Empty;
      
      var actualCommand = command with { TenantId = tenantId, CustomerId = userId };
      var result = await mediator.Send(actualCommand);
      return result.IsSuccess
              ? Results.Created($"/api/v1/tickets/{result.Value!.Id}", result.Value)
              : Results.BadRequest(result.Error);
    });

    group.MapPatch("/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, UpdateTicketCommand command, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var actualCommand = new UpdateTicketCommand(tenantId, id, command.Title, command.Description, command.Priority, command.Status, command.AssignedAgentId);
      var result = await mediator.Send(actualCommand);
      return result.IsSuccess ? Results.Ok() : Results.NotFound(result.Error);
    });

    group.MapPatch("/{id:guid}/status", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, ChangeTicketStatusCommand command, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var actualCommand = new ChangeTicketStatusCommand(tenantId, id, command.NewStatus);
      var result = await mediator.Send(actualCommand);
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    group.MapPatch("/{id:guid}/assign", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, AssignTicketCommand command, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var actualCommand = new AssignTicketCommand(tenantId, id, command.AgentId);
      var result = await mediator.Send(actualCommand);
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    group.MapDelete("/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var dto = await mediator.Send(new GetTicketByIdQuery(tenantId, id));
      if (dto.Value is null) return Results.NotFound();
      return Results.NoContent();
    });

    return app;
  }
}
