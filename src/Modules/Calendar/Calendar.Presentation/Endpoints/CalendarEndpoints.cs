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

    group.MapGet("", async (System.Security.Claims.ClaimsPrincipal principal, DateTime? startDate, DateTime? endDate, string? type, IMediator mediator, int page = 1, int pageSize = 25) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var query = new GetEventsQuery(tenantId, startDate, endDate, type, new() { Page = page, PageSize = pageSize });
      var result = await mediator.Send(query);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var result = await mediator.Send(new GetEventByIdQuery(tenantId, id));
      return result.Value is null ? Results.NotFound() : Results.Ok(result.Value);
    });

    group.MapPost("", async (CreateCalendarEventCommand command, IMediator mediator) =>
    {
      var result = await mediator.Send(command);
      return result.IsSuccess
              ? Results.Created($"/api/v1/events/{result.Value!.Id}", result.Value)
              : Results.BadRequest(result.Error);
    });

    group.MapPatch("/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, UpdateCalendarEventCommand command, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var actualCommand = new UpdateCalendarEventCommand(tenantId, id, tenantId, command.Title, command.StartTime, command.EndTime, command.Description, command.Location);
      var result = await mediator.Send(actualCommand);
      return result.IsSuccess ? Results.Ok() : Results.NotFound(result.Error);
    });

    group.MapPatch("/{id:guid}/reschedule", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, DateTime newStartTime, DateTime newEndTime, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var command = new RescheduleEventCommand(tenantId, id, tenantId, newStartTime, newEndTime);
      var result = await mediator.Send(command);
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    group.MapDelete("/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var result = await mediator.Send(new CancelEventCommand(tenantId, id, tenantId));
      return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
    });

    return app;
  }
}
