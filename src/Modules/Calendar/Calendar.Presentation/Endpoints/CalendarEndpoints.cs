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
    group.MapGet("", async (IUserContext usuario, DateTime? startDate, DateTime? endDate, string? type,
                            IMediator mediator, int page = 1, int pageSize = 200) =>
    {
      var query = new GetEventsQuery(usuario.TenantId, startDate, endDate, type, new() { Page = page, PageSize = pageSize });
      var result = await mediator.Send(query);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/{id:guid}", async (IUserContext usuario, Guid id, IMediator mediator) =>
    {
      var result = await mediator.Send(new GetEventByIdQuery(usuario.TenantId, id));
      return result.Value is null ? Results.NotFound() : Results.Ok(result.Value);
    });

    group.MapPost("", async (CreateCalendarEventCommand command, IUserContext usuario, IMediator mediator) =>
    {
      var result = await mediator.Send(command with
      {
        TenantId = usuario.TenantId,
        OrganizerId = usuario.UserId,
      });

      return result.IsSuccess
              ? Results.Created($"/api/v1/calendar/events/{result.Value!.Id}", result.Value)
              : Results.BadRequest(result.Error);
    });

    group.MapPatch("/{id:guid}", async (Guid id, UpdateCalendarEventCommand command,
                                        IUserContext usuario, IMediator mediator) =>
    {
      // Con nombres, y `ActorId` es el usuario. Antes se construía posicionalmente
      // —`new UpdateCalendarEventCommand(tenantId, id, tenantId, ...)`— y el inquilino acababa
      // guardado como si fuera quien editó. Es el mismo cruce que ya apareció en el panel y en el
      // borrado de proyectos: `Guid` seguidos que compilan en cualquier orden.
      var result = await mediator.Send(command with
      {
        TenantId = usuario.TenantId,
        EventId = id,
        ActorId = usuario.UserId
      });

      return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
    });

    group.MapPatch("/{id:guid}/reschedule", async (Guid id, DateTime newStartTime, DateTime newEndTime,
                                                   IUserContext usuario, IMediator mediator) =>
    {
      var command = new RescheduleEventCommand(usuario.TenantId, id, usuario.UserId, newStartTime, newEndTime);
      var result = await mediator.Send(command);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    /// Anular: el evento se queda en el calendario, tachado.
    group.MapPost("/{id:guid}/anular", async (Guid id, AnularEventoRequest? cuerpo,
                                              IUserContext usuario, IMediator mediator) =>
    {
      var result = await mediator.Send(
          new AnularEventoCommand(usuario.TenantId, id, usuario.UserId, cuerpo?.Motivo));

      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapPost("/{id:guid}/reactivar", async (Guid id, IUserContext usuario, IMediator mediator) =>
    {
      var result = await mediator.Send(new ReactivarEventoCommand(usuario.TenantId, id, usuario.UserId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapPut("/{id:guid}/enlaces", async (Guid id, EnlacesDelEventoRequest cuerpo,
                                              IUserContext usuario, IMediator mediator) =>
    {
      // PUT y no PATCH: se manda el juego entero de enlaces, incluidos los que van en nulo. Con
      // PATCH no habría forma de distinguir «quita el enlace» de «no toques este campo».
      var result = await mediator.Send(
          new EnlazarEventoCommand(usuario.TenantId, id, cuerpo.ProjectId, cuerpo.TaskId, cuerpo.TicketId));

      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    /// A la papelera. Recuperable con `/restaurar`.
    group.MapDelete("/{id:guid}", async (Guid id, IUserContext usuario, IMediator mediator) =>
    {
      var result = await mediator.Send(new CancelEventCommand(usuario.TenantId, id, usuario.UserId));
      return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
    });

    group.MapGet("/papelera", async (IUserContext usuario, IMediator mediator, int page = 1, int pageSize = 50) =>
    {
      var result = await mediator.Send(
          new GetEventosEnPapeleraQuery(usuario.TenantId, new() { Page = page, PageSize = pageSize }));

      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapPost("/{id:guid}/restaurar", async (Guid id, IUserContext usuario, IMediator mediator) =>
    {
      var result = await mediator.Send(new RestoreEventCommand(usuario.TenantId, id, usuario.UserId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    return app;
  }
}

/// <param name="Motivo">Por qué se anula. Opcional: a veces no hay más que decir.</param>
public sealed record AnularEventoRequest(string? Motivo);

/// <summary>
/// Los enlaces del evento, los tres a la vez. Un nulo significa «sin enlace», no «no lo cambies».
/// </summary>
public sealed record EnlacesDelEventoRequest(Guid? ProjectId, Guid? TaskId, Guid? TicketId);
