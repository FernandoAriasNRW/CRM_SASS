using System.Linq;
using BuildingBlocks.Application.Abstractions;
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
  /// <summary>Las rutas de archivo y papelera, con la acción que ejecuta cada una.</summary>
  private static readonly (string Ruta, BuildingBlocks.Application.ArchiveAction Accion)[] ArchiveActions =
  [
    ("archive", BuildingBlocks.Application.ArchiveAction.Archive),
    ("unarchive", BuildingBlocks.Application.ArchiveAction.Unarchive),
    ("restore", BuildingBlocks.Application.ArchiveAction.RestoreFromTrash)
  ];

  /// <summary>
  /// El mensaje con el que los handlers dicen que la tarea no existe. Es lo único que separa un
  /// 404 de un 400, así que se nombra en lugar de repetir la cadena.
  /// </summary>
  private const string TaskNotFound = "Tarea no encontrada";

  public static IServiceCollection AddWorkItemsPresentation(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddWorkItemsInfrastructure(configuration);
    return services;
  }

  public static IEndpointRouteBuilder MapWorkItemsEndpoints(this IEndpointRouteBuilder app)
  {
    var group = app.MapGroup("/api/v1/tasks").WithTags("Tasks").RequireAuthorization();

    group.MapGet("", async (IUserContext currentUser, [Microsoft.AspNetCore.Mvc.FromQuery] Guid? projectId, [Microsoft.AspNetCore.Mvc.FromQuery] Guid? assigneeId, [Microsoft.AspNetCore.Mvc.FromQuery] string? status, [Microsoft.AspNetCore.Mvc.FromQuery] string? priority, [Microsoft.AspNetCore.Mvc.FromQuery] string? filter, BuildingBlocks.Application.Abstractions.IViewScopeResolver viewScopes, IMediator mediator, [Microsoft.AspNetCore.Mvc.FromQuery] Guid? parentTaskId = null, [Microsoft.AspNetCore.Mvc.FromQuery] bool includeSubtasks = false, [Microsoft.AspNetCore.Mvc.FromQuery] int page = 1, [Microsoft.AspNetCore.Mvc.FromQuery] int pageSize = 25, [Microsoft.AspNetCore.Mvc.FromQuery] string? sortColumn = null, [Microsoft.AspNetCore.Mvc.FromQuery] string? sortDirection = null, [Microsoft.AspNetCore.Mvc.FromQuery] DateTime? startDate = null, [Microsoft.AspNetCore.Mvc.FromQuery] DateTime? endDate = null, [Microsoft.AspNetCore.Mvc.FromQuery] string? search = null) =>
    {
      var tenantId = currentUser.TenantId;
      var userId = currentUser.UserId;
      // El resolutor decide qué hace falta consultar para el filtro pedido —favoritos,
      // compartidos, nada— en un solo sitio, para que ningún endpoint reciba un filtro y no
      // haga nada con él, que es como «Mis Tickets» acabó devolviendo los 175 de siempre.
      var viewScope = await viewScopes.ResolveAsync(filter, userId, BuildingBlocks.Domain.EntityTypes.Task);

      var query = new GetTasksQuery(tenantId, projectId, assigneeId, status, priority, viewScope, new() { Page = page, PageSize = pageSize, SortColumn = sortColumn, SortDirection = sortDirection, StartDate = startDate, EndDate = endDate, Search = search }, parentTaskId, includeSubtasks);
      var result = await mediator.Send(query);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/{id:guid}", async (IUserContext currentUser, Guid id, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var result = await mediator.Send(new GetTaskByIdQuery(tenantId, id));
      return result.Value is null ? Results.NotFound() : Results.Ok(result.Value);
    });

    // El inquilino y el autor salen del token, no del cuerpo. Ver el comentario largo en
    // ProjectsEndpoints: era la misma grieta, y permitía crear tareas dentro de la
    // organización de otro sin más que poner un Guid en el JSON.
    group.MapPost("", async (CreateTaskCommand command, IUserContext user, IMediator mediator) =>
    {
      var result = await mediator.Send(command with
      {
        TenantId = user.TenantId,
        CreatedById = user.UserId,
      });

      return result.IsSuccess
              ? Results.Created($"/api/v1/tasks/{result.Value!.Id}", result.Value)
              : Results.BadRequest(result.Error);
    });

    // Quién mueve la tarea y con qué rol sale del token.
    //
    // Llegaban por la cadena de consulta —`?actorId=…&actorRole=…`— y el manejador autoriza con
    // exactamente esto:
    //
    //     if (request.ActorRole != "Admin" && task.AssigneeId != request.ActorId) …
    //
    // Es decir, bastaba añadir `&actorRole=Admin` a la URL para saltarse la comprobación entera
    // y mover cualquier tarea del inquilino. Todos los demás endpoints de este archivo ya leían
    // el actor de las reclamaciones; éste se había quedado atrás.
    // El estado nuevo viaja en el cuerpo, y la ruta acepta PATCH y POST.
    //
    // No es capricho: el tablero llamaba desde siempre con `POST` y un cuerpo
    // `{ newStatus }`, mientras que aquí sólo había un `PATCH` que leía `?status=`. Cada
    // arrastre de una tarjeta se topaba con un 405, la tarjeta volvía a su columna por el
    // camino de revertir y salía un aviso de error. **Arrastrar en el tablero no ha
    // funcionado nunca.**
    //
    // Se arregla por el lado del servidor además de por el del cliente: el cuerpo es lo que
    // usa el resto de la API, y admitir los dos verbos evita que una versión antigua de la
    // interfaz servida desde una caché se quede rota.
    var move = async (Guid id, MoveTaskRequest body, IUserContext user, IMediator mediator) =>
    {
      var result = await mediator.Send(
          new MoveTaskCommand(user.TenantId, id, user.UserId, user.Role, body.NewStatus));

      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    };

    group.MapPatch("/{id:guid}/move", move);
    group.MapPost("/{id:guid}/move", move);

    group.MapPatch("/{id:guid}", async (IUserContext currentUser, Guid id, PatchTaskCommand command, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var actorId = currentUser.UserId;
      var actorRole = currentUser.Role;
      
      // `with` en lugar de reconstruir el comando campo a campo: la lista posicional se quedó
      // corta al añadir las horas, y un campo olvidado aquí no da error de compilación —llega
      // como nulo y el cambio se pierde sin que nadie se entere—.
      var actualCommand = command with
      {
        TenantId = tenantId,
        Id = id,
        ActorId = actorId,
        ActorRole = actorRole
      };

      var result = await mediator.Send(actualCommand);

      if (result.IsSuccess) return Results.Ok();

      // Un valor que el dominio rechaza no es un 404: quien lo lea entendería que la tarea no
      // existe y buscaría el fallo donde no está.
      return result.Error == TaskNotFound
          ? Results.NotFound(result.Error)
          : Results.BadRequest(result.Error);
    });

    // Las subtareas de una tarea. Es el mismo listado con el filtro puesto, para que la
    // paginación y el orden funcionen igual que en cualquier otra vista.
    group.MapGet("/{id:guid}/subtasks", async (IUserContext currentUser, Guid id, IMediator mediator, [Microsoft.AspNetCore.Mvc.FromQuery] int page = 1, [Microsoft.AspNetCore.Mvc.FromQuery] int pageSize = 100, [Microsoft.AspNetCore.Mvc.FromQuery] string? sortColumn = null, [Microsoft.AspNetCore.Mvc.FromQuery] string? sortDirection = null) =>
    {
      var tenantId = currentUser.TenantId;
      var userId = currentUser.UserId;
      var query = new GetTasksQuery(tenantId, null, null, null, null, null, new() { Page = page, PageSize = pageSize, SortColumn = sortColumn, SortDirection = sortDirection }, id, false);
      var result = await mediator.Send(query);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    // Colgar la tarea de otra, o desligarla enviando parentTaskId nulo.
    group.MapPatch("/{id:guid}/parent", async (IUserContext currentUser, Guid id, ReparentTaskCommand command, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var actorId = currentUser.UserId;
      var actorRole = currentUser.Role;

      var result = await mediator.Send(new ReparentTaskCommand(tenantId, id, actorId, actorRole, command.ParentTaskId));
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    // Recurrencia. Es un patrón de la tarea, así que se pone y se quita entera; no hay «patch
    // parcial» porque cambiar sólo el intervalo sin decir desde cuándo no significa nada claro.
    group.MapPut("/{id:guid}/recurrence", async (IUserContext currentUser, Guid id, SetTaskRecurrenceCommand command, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var actorId = currentUser.UserId;
      var actorRole = currentUser.Role;

      var result = await mediator.Send(new SetTaskRecurrenceCommand(
          tenantId, id, actorId, actorRole, command.Frequency, command.Interval,
          command.NextOccurrence, command.EndDate));

      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    group.MapDelete("/{id:guid}/recurrence", async (IUserContext currentUser, Guid id, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var actorId = currentUser.UserId;
      var actorRole = currentUser.Role;

      var result = await mediator.Send(new ClearTaskRecurrenceCommand(tenantId, id, actorId, actorRole));
      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
    });

    // Checklist. Los puntos se piden aparte del listado: en la tarjeta basta con el progreso, y
    // traer todos los textos de todas las tareas para pintar «2/5» sería cargar de más.
    group.MapGet("/{id:guid}/checklist", async (IUserContext currentUser, Guid id, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var result = await mediator.Send(new GetChecklistQuery(tenantId, id));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapPost("/{id:guid}/checklist", async (IUserContext currentUser, Guid id, AddChecklistItemCommand command, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var actorId = currentUser.UserId;
      var actorRole = currentUser.Role;

      var result = await mediator.Send(new AddChecklistItemCommand(tenantId, id, actorId, actorRole, command.Text));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapPatch("/{id:guid}/checklist/{itemId:guid}", async (IUserContext currentUser, Guid id, Guid itemId, UpdateChecklistItemCommand command, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var actorId = currentUser.UserId;
      var actorRole = currentUser.Role;

      var result = await mediator.Send(new UpdateChecklistItemCommand(tenantId, id, actorId, actorRole, itemId, command.IsDone, command.Text));
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    group.MapDelete("/{id:guid}/checklist/{itemId:guid}", async (IUserContext currentUser, Guid id, Guid itemId, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var actorId = currentUser.UserId;
      var actorRole = currentUser.Role;

      var result = await mediator.Send(new RemoveChecklistItemCommand(tenantId, id, actorId, actorRole, itemId));
      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
    });

    // Responsables. Cambiar el principal sigue haciéndose con el patch de la tarea; esto añade y
    // quita gente del conjunto, que es otra intención.
    group.MapPost("/{id:guid}/assignees", async (IUserContext currentUser, Guid id, AddTaskAssigneeCommand command, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var actorId = currentUser.UserId;
      var actorRole = currentUser.Role;

      var result = await mediator.Send(new AddTaskAssigneeCommand(tenantId, id, actorId, actorRole, command.UserId));
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    group.MapDelete("/{id:guid}/assignees/{userId:guid}", async (IUserContext currentUser, Guid id, Guid userId, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var actorId = currentUser.UserId;
      var actorRole = currentUser.Role;

      var result = await mediator.Send(new RemoveTaskAssigneeCommand(tenantId, id, actorId, actorRole, userId));
      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
    });

    // El grafo entero, para el Gantt. Va antes que la ruta con identificador para que no la
    // capture: «dependencies» no es un Guid, pero dejar dos rutas que compiten por el mismo
    // tramo es la clase de cosa que se rompe sola al tocar cualquiera de las dos.
    group.MapGet("/dependencies", async (IUserContext currentUser, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var result = await mediator.Send(new GetDependencyGraphQuery(tenantId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    // Dependencias: las dos direcciones en una sola respuesta, porque el panel las pinta juntas.
    group.MapGet("/{id:guid}/dependencies", async (IUserContext currentUser, Guid id, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var result = await mediator.Send(new GetTaskDependenciesQuery(tenantId, id));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapPost("/{id:guid}/dependencies", async (IUserContext currentUser, Guid id, AddTaskDependencyCommand command, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var actorId = currentUser.UserId;
      var actorRole = currentUser.Role;

      var result = await mediator.Send(new AddTaskDependencyCommand(tenantId, id, actorId, actorRole, command.DependsOnTaskId));
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    group.MapDelete("/{id:guid}/dependencies/{dependsOnTaskId:guid}", async (IUserContext currentUser, Guid id, Guid dependsOnTaskId, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var actorId = currentUser.UserId;
      var actorRole = currentUser.Role;

      var result = await mediator.Send(new RemoveTaskDependencyCommand(tenantId, id, actorId, actorRole, dependsOnTaskId));
      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
    });

    // Archivar, desarchivar y restaurar de la papelera. En una tabla y no en tres bloques
    // copiados: son idénticos salvo el verbo, y copiarlos es como acaban desincronizándose.
    // Borrar sigue siendo DELETE, que ahora sí manda a la papelera.
    foreach (var (route, action) in ArchiveActions)
    {
      group.MapPost("/{id:guid}/" + route, async (IUserContext currentUser, Guid id, IMediator mediator) =>
      {
        var tenantId = currentUser.TenantId;

        var result = await mediator.Send(new WorkItems.Application.ChangeTaskArchiveStateCommand(tenantId, id, action));
        return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
      });
    }

    group.MapDelete("/{id:guid}", async (IUserContext currentUser, Guid id, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var actorId = currentUser.UserId;
      var actorRole = currentUser.Role;
      
      var result = await mediator.Send(new DeleteTaskCommand(tenantId, id, actorId, actorRole));
      return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
    });

    return app;
  }
}

/// <summary>
/// El cuerpo de «mover una tarea». El nombre del campo es el que ya mandaba el tablero.
/// </summary>
public sealed record MoveTaskRequest(string NewStatus);
