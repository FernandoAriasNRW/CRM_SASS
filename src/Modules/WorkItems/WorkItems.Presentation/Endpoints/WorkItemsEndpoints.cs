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

    group.MapGet("", async (System.Security.Claims.ClaimsPrincipal principal, [Microsoft.AspNetCore.Mvc.FromQuery] Guid? projectId, [Microsoft.AspNetCore.Mvc.FromQuery] Guid? assigneeId, [Microsoft.AspNetCore.Mvc.FromQuery] string? status, [Microsoft.AspNetCore.Mvc.FromQuery] string? priority, [Microsoft.AspNetCore.Mvc.FromQuery] string? filter, IMediator mediator, [Microsoft.AspNetCore.Mvc.FromQuery] Guid? parentTaskId = null, [Microsoft.AspNetCore.Mvc.FromQuery] bool includeSubtasks = false, [Microsoft.AspNetCore.Mvc.FromQuery] int page = 1, [Microsoft.AspNetCore.Mvc.FromQuery] int pageSize = 25, [Microsoft.AspNetCore.Mvc.FromQuery] string? sortColumn = null, [Microsoft.AspNetCore.Mvc.FromQuery] string? sortDirection = null, [Microsoft.AspNetCore.Mvc.FromQuery] DateTime? startDate = null, [Microsoft.AspNetCore.Mvc.FromQuery] DateTime? endDate = null) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var userId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;
      var query = new GetTasksQuery(tenantId, projectId, assigneeId, status, priority, filter, userId, new() { Page = page, PageSize = pageSize, SortColumn = sortColumn, SortDirection = sortDirection, StartDate = startDate, EndDate = endDate }, parentTaskId, includeSubtasks);
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

    // Las subtareas de una tarea. Es el mismo listado con el filtro puesto, para que la
    // paginación y el orden funcionen igual que en cualquier otra vista.
    group.MapGet("/{id:guid}/subtasks", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator, [Microsoft.AspNetCore.Mvc.FromQuery] int page = 1, [Microsoft.AspNetCore.Mvc.FromQuery] int pageSize = 100, [Microsoft.AspNetCore.Mvc.FromQuery] string? sortColumn = null, [Microsoft.AspNetCore.Mvc.FromQuery] string? sortDirection = null) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var userId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;
      var query = new GetTasksQuery(tenantId, null, null, null, null, null, userId, new() { Page = page, PageSize = pageSize, SortColumn = sortColumn, SortDirection = sortDirection }, id, false);
      var result = await mediator.Send(query);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    // Colgar la tarea de otra, o desligarla enviando parentTaskId nulo.
    group.MapPatch("/{id:guid}/parent", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, ReparentTaskCommand command, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var actorId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;
      var actorRole = principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;

      var result = await mediator.Send(new ReparentTaskCommand(tenantId, id, actorId, actorRole, command.ParentTaskId));
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    // Checklist. Los puntos se piden aparte del listado: en la tarjeta basta con el progreso, y
    // traer todos los textos de todas las tareas para pintar «2/5» sería cargar de más.
    group.MapGet("/{id:guid}/checklist", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var result = await mediator.Send(new GetChecklistQuery(tenantId, id));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapPost("/{id:guid}/checklist", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, AddChecklistItemCommand command, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var actorId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;
      var actorRole = principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;

      var result = await mediator.Send(new AddChecklistItemCommand(tenantId, id, actorId, actorRole, command.Texto));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapPatch("/{id:guid}/checklist/{itemId:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, Guid itemId, UpdateChecklistItemCommand command, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var actorId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;
      var actorRole = principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;

      var result = await mediator.Send(new UpdateChecklistItemCommand(tenantId, id, actorId, actorRole, itemId, command.Hecho, command.Texto));
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    group.MapDelete("/{id:guid}/checklist/{itemId:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, Guid itemId, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var actorId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;
      var actorRole = principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;

      var result = await mediator.Send(new RemoveChecklistItemCommand(tenantId, id, actorId, actorRole, itemId));
      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
    });

    // Responsables. Cambiar el principal sigue haciéndose con el patch de la tarea; esto añade y
    // quita gente del conjunto, que es otra intención.
    group.MapPost("/{id:guid}/assignees", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, AddTaskAssigneeCommand command, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var actorId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;
      var actorRole = principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;

      var result = await mediator.Send(new AddTaskAssigneeCommand(tenantId, id, actorId, actorRole, command.UserId));
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    group.MapDelete("/{id:guid}/assignees/{userId:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, Guid userId, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var actorId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;
      var actorRole = principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;

      var result = await mediator.Send(new RemoveTaskAssigneeCommand(tenantId, id, actorId, actorRole, userId));
      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
    });

    // Dependencias: las dos direcciones en una sola respuesta, porque el panel las pinta juntas.
    group.MapGet("/{id:guid}/dependencies", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var result = await mediator.Send(new GetTaskDependenciesQuery(tenantId, id));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapPost("/{id:guid}/dependencies", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, AddTaskDependencyCommand command, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var actorId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;
      var actorRole = principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;

      var result = await mediator.Send(new AddTaskDependencyCommand(tenantId, id, actorId, actorRole, command.DependsOnTaskId));
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    group.MapDelete("/{id:guid}/dependencies/{dependsOnTaskId:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, Guid dependsOnTaskId, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var actorId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;
      var actorRole = principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;

      var result = await mediator.Send(new RemoveTaskDependencyCommand(tenantId, id, actorId, actorRole, dependsOnTaskId));
      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
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
