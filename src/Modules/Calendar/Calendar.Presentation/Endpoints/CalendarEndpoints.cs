using BuildingBlocks.Application.Abstractions;
using System.Linq;
using Calendar.Application.Commands;
using Calendar.Application.Queries;
using Calendar.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Calendar.Presentation.Endpoints;

public static class CalendarEndpoints
{
  public static IServiceCollection AddCalendarPresentation(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddCalendarInfrastructure(configuration);
    return services;
  }

  public static IEndpointRouteBuilder MapCalendarEndpoints(this IEndpointRouteBuilder app)
  {
    var group = app.MapGroup("/api/v1/calendar/events").WithTags("Calendar").RequireAuthorization();

    // El inquilino y el usuario salen de `IUserContext` y no de leer las claims a mano en cada
    // endpoint. Leerlas a mano es lo que había, y es de donde salió el fallo de abajo: se pasaba
    // el `tenantId` en el hueco del usuario porque las dos variables son `Guid` y nadie se queja.
    group.MapGet("", async (IUserContext currentUser, DateTime? startDate, DateTime? endDate, string? type,
                            IMediator mediator, int page = 1, int pageSize = 200) =>
    {
      var query = new GetEventsQuery(currentUser.TenantId, startDate, endDate, type, new() { Page = page, PageSize = pageSize });
      var result = await mediator.Send(query);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/{id:guid}", async (IUserContext currentUser, Guid id, IMediator mediator) =>
    {
      var result = await mediator.Send(new GetEventByIdQuery(currentUser.TenantId, id));
      return result.Value is null ? Results.NotFound() : Results.Ok(result.Value);
    });

    group.MapPost("", async (CreateCalendarEventCommand command, IUserContext currentUser, IMediator mediator) =>
    {
      var result = await mediator.Send(command with
      {
        TenantId = currentUser.TenantId,
        OrganizerId = currentUser.UserId,
      });

      return result.IsSuccess
              ? Results.Created($"/api/v1/calendar/events/{result.Value!.Id}", result.Value)
              : Results.BadRequest(result.Error);
    });

    group.MapPatch("/{id:guid}", async (Guid id, UpdateCalendarEventCommand command,
                                        IUserContext currentUser, IMediator mediator) =>
    {
      // Con nombres, y `ActorId` es el usuario. Antes se construía posicionalmente
      // —`new UpdateCalendarEventCommand(tenantId, id, tenantId, ...)`— y el inquilino acababa
      // guardado como si fuera quien editó. Es el mismo cruce que ya apareció en el panel y en el
      // borrado de proyectos: `Guid` seguidos que compilan en cualquier orden.
      var result = await mediator.Send(command with
      {
        TenantId = currentUser.TenantId,
        EventId = id,
        ActorId = currentUser.UserId
      });

      return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
    });

    group.MapPatch("/{id:guid}/reschedule", async (Guid id, DateTime newStartTime, DateTime newEndTime,
                                                   IUserContext currentUser, IMediator mediator) =>
    {
      var command = new RescheduleEventCommand(currentUser.TenantId, id, currentUser.UserId, newStartTime, newEndTime);
      var result = await mediator.Send(command);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    /// Anular: el evento se queda en el calendario, tachado.
    group.MapPost("/{id:guid}/cancel", async (Guid id, CancelEventRequest? body,
                                              IUserContext currentUser, IMediator mediator) =>
    {
      var result = await mediator.Send(
          new CancelEventCommand(currentUser.TenantId, id, currentUser.UserId, body?.Reason));

      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapPost("/{id:guid}/reactivate", async (Guid id, IUserContext currentUser, IMediator mediator) =>
    {
      var result = await mediator.Send(new ReactivateEventCommand(currentUser.TenantId, id, currentUser.UserId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapPut("/{id:guid}/links", async (Guid id, EventLinksRequest body,
                                              IUserContext currentUser, IMediator mediator) =>
    {
      // PUT y no PATCH: se manda el juego entero de enlaces, incluidos los que van en nulo. Con
      // PATCH no habría forma de distinguir «quita el enlace» de «no toques este campo».
      var result = await mediator.Send(
          new LinkEventCommand(currentUser.TenantId, id, body.ProjectId, body.TaskId, body.TicketId));

      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    /// A la papelera. Recuperable con `/restore`.
    group.MapDelete("/{id:guid}", async (Guid id, IUserContext currentUser, IMediator mediator) =>
    {
      var result = await mediator.Send(new MoveEventToTrashCommand(currentUser.TenantId, id, currentUser.UserId));
      return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
    });

    group.MapGet("/trash", async (IUserContext currentUser, IMediator mediator, int page = 1, int pageSize = 50) =>
    {
      var result = await mediator.Send(
          new GetTrashedEventsQuery(currentUser.TenantId, new() { Page = page, PageSize = pageSize }));

      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapPost("/{id:guid}/restore", async (Guid id, IUserContext currentUser, IMediator mediator) =>
    {
      var result = await mediator.Send(new RestoreEventCommand(currentUser.TenantId, id, currentUser.UserId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    return app;
  }
}

/// <param name="Reason">Por qué se anula. Opcional: a veces no hay más que decir.</param>
public sealed record CancelEventRequest(string? Reason);

/// <summary>
/// Los enlaces del evento, los tres a la vez. Un nulo significa «sin enlace», no «no lo cambies».
/// </summary>
public sealed record EventLinksRequest(Guid? ProjectId, Guid? TaskId, Guid? TicketId);
