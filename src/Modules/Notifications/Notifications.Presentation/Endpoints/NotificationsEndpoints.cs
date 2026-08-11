using System.Linq;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.Commands;
using Notifications.Application.Queries;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Persistence;

namespace Notifications.Presentation.Endpoints;

public static class NotificationsEndpoints
{
  public static IServiceCollection AddNotificationsPresentation(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddNotificationsInfrastructure(configuration);
    return services;
  }

  public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
  {
    var group = app.MapGroup("/api/v1/notifications").WithTags("Notifications").RequireAuthorization();

    group.MapGet("", async (System.Security.Claims.ClaimsPrincipal principal, Guid? recipientId, string? type, string? status, IMediator mediator, int page = 1, int pageSize = 25) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var query = new GetNotificationsQuery(tenantId, recipientId, type, status, new() { Page = page, PageSize = pageSize });
      var result = await mediator.Send(query);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var query = new GetNotificationByIdQuery(tenantId, id);
      var result = await mediator.Send(query);
      return result.Value is null ? Results.NotFound() : Results.Ok(result.Value);
    });

    group.MapGet("/unread-count", async (System.Security.Claims.ClaimsPrincipal principal, Guid recipientId, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var result = await mediator.Send(new GetUnreadCountQuery(tenantId, recipientId));
      return Results.Ok(new { Count = result });
    });

    group.MapPost("", async (CreateNotificationCommand command, IMediator mediator) =>
    {
      var result = await mediator.Send(command);
      return result.IsSuccess
              ? Results.Created($"/api/v1/notifications/{result.Value!.Id}", result.Value)
              : Results.BadRequest(result.Error);
    });

    group.MapPost("/{id:guid}/read", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, Guid? recipientId, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var userId = recipientId ?? (Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub")?.Value, out var _uid) ? _uid : Guid.Empty);
      var result = await mediator.Send(new MarkNotificationAsReadCommand(tenantId, id, userId));
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    group.MapPatch("/{id:guid}/read", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, Guid? recipientId, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var userId = recipientId ?? (Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub")?.Value, out var _uid) ? _uid : Guid.Empty);
      var result = await mediator.Send(new MarkNotificationAsReadCommand(tenantId, id, userId));
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    group.MapPost("/read-all", async (System.Security.Claims.ClaimsPrincipal principal, NotificationsDbContext dbContext) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var userId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub")?.Value, out var _uid) ? _uid : Guid.Empty;
      
      var notifs = await dbContext.Notifications
          .Where(n => n.TenantId == tenantId && (n.RecipientUserId == userId || userId == Guid.Empty) && n.StatusValue != "Read" && !n.IsDeleted)
          .ToListAsync();

      foreach (var n in notifs)
      {
          n.MarkAsRead();
      }

      await dbContext.SaveChangesAsync();
      return Results.Ok();
    });

    group.MapDelete("/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var userId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub")?.Value, out var _uid) ? _uid : Guid.Empty;
      var command = new DeleteNotificationCommand(tenantId, id, userId);
      var result = await mediator.Send(command);
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    group.MapGet("/preferences", () =>
    {
      return Results.Ok(new
      {
        EmailEnabled = true,
        PushEnabled = false,
        TaskAssigned = true,
        TaskCompleted = false,
        TaskDueSoon = true,
        TicketCreated = true,
        TicketUpdated = false,
        ProjectUpdated = true,
        MentionEnabled = true,
        QuietHoursEnabled = false,
        QuietHoursStart = "22:00",
        QuietHoursEnd = "08:00"
      });
    });

    group.MapPut("/preferences", (object prefs) =>
    {
      return Results.Ok(prefs);
    });

    return app;
  }
}
