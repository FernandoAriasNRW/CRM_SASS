using System.Linq;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkItems.Application.Commands;
using WorkItems.Application.Queries;
using WorkItems.Infrastructure;

namespace WorkItems.Presentation.Endpoints;

public static class WorkItemsEndpoints
{
  public static IServiceCollection AddWorkItemsPresentation(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddWorkItemsInfrastructure(configuration);
    return services;
  }

  public static IEndpointRouteBuilder MapWorkItemsEndpoints(this IEndpointRouteBuilder app)
  {
    var group = app.MapGroup("/api/v1/tasks").WithTags("Tasks").RequireAuthorization();

    group.MapGet("", async (System.Security.Claims.ClaimsPrincipal principal, [Microsoft.AspNetCore.Mvc.FromQuery] Guid? projectId, [Microsoft.AspNetCore.Mvc.FromQuery] Guid? assigneeId, [Microsoft.AspNetCore.Mvc.FromQuery] string? status, [Microsoft.AspNetCore.Mvc.FromQuery] string? priority, [Microsoft.AspNetCore.Mvc.FromQuery] string? filter, IMediator mediator, [Microsoft.AspNetCore.Mvc.FromQuery] int page = 1, [Microsoft.AspNetCore.Mvc.FromQuery] int pageSize = 25, [Microsoft.AspNetCore.Mvc.FromQuery] string? sortColumn = null, [Microsoft.AspNetCore.Mvc.FromQuery] string? sortDirection = null, [Microsoft.AspNetCore.Mvc.FromQuery] DateTime? startDate = null, [Microsoft.AspNetCore.Mvc.FromQuery] DateTime? endDate = null) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var userId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;
      var query = new GetTasksQuery(tenantId, projectId, assigneeId, status, priority, filter, userId, new() { Page = page, PageSize = pageSize, SortColumn = sortColumn, SortDirection = sortDirection, StartDate = startDate, EndDate = endDate });
      var result = await mediator.Send(query);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var result = await mediator.Send(new GetTaskByIdQuery(tenantId, id));
      return result.Value is null ? Results.NotFound() : Results.Ok(result.Value);
    });

    group.MapPost("", async (CreateTaskCommand command, IMediator mediator) =>
    {
      var result = await mediator.Send(command);
      return result.IsSuccess
              ? Results.Created($"/api/v1/tasks/{result.Value!.Id}", result.Value)
              : Results.BadRequest(result.Error);
    });

    group.MapPatch("/{id:guid}/move", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, string status, Guid actorId, string actorRole, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var result = await mediator.Send(new MoveTaskCommand(tenantId, id, actorId, actorRole, status));
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    group.MapPatch("/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, PatchTaskCommand command, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var actorId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;
      var actorRole = principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;
      
      var actualCommand = new PatchTaskCommand(tenantId, id, actorId, actorRole, command.Title, command.Description, command.Status, command.Priority, command.AssigneeId, command.DueDate);
      var result = await mediator.Send(actualCommand);
      return result.IsSuccess ? Results.Ok() : Results.NotFound(result.Error);
    });

    group.MapDelete("/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var actorId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;
      var actorRole = principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;
      
      var result = await mediator.Send(new DeleteTaskCommand(tenantId, id, actorId, actorRole));
      return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
    });

    return app;
  }
}
