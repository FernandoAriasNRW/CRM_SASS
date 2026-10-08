using BuildingBlocks.Application.Abstractions;
using System.Linq;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.Commands;
using Notifications.Application.Preferences;
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

    // Los avisos de quien pregunta, y sólo los suyos. El destinatario llegaba por la URL y era
    // opcional: sin él salían los avisos de toda la organización, que es justo lo que pedía la
    // pantalla. Cada persona veía los de todas las demás.
    group.MapGet("", async (IUserContext currentUser, string? type, string? status, IMediator mediator, int page = 1, int pageSize = 25) =>
    {
      var tenantId = currentUser.TenantId;
      var query = new GetNotificationsQuery(tenantId, currentUser.UserId, type, status, new() { Page = page, PageSize = pageSize });
      var result = await mediator.Send(query);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/{id:guid}", async (IUserContext currentUser, Guid id, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var query = new GetNotificationByIdQuery(tenantId, id);
      var result = await mediator.Send(query);
      return result.Value is null || result.Value.RecipientUserId != currentUser.UserId
          ? Results.NotFound()
          : Results.Ok(result.Value);
    });

    group.MapGet("/unread-count", async (IUserContext currentUser, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var result = await mediator.Send(new GetUnreadCountQuery(tenantId, currentUser.UserId));
      return Results.Ok(new { Count = result });
    });

    // Inquilino y remitente, del token. Ver ProjectsEndpoints: misma grieta.
    //
    // Aquí el destinatario sí llega en el cuerpo, y debe seguir así: notificar a otra persona
    // es justo lo que hace este endpoint. Lo que no puede elegir quien llama es *en qué
    // organización* deja la notificación, ni firmarla con el nombre de otro.
    group.MapPost("", async (CreateNotificationCommand command, IUserContext currentUser, IMediator mediator) =>
    {
      var result = await mediator.Send(command with
      {
        TenantId = currentUser.TenantId,
        SenderUserId = currentUser.UserId,
      });

      return result.IsSuccess
              ? Results.Created($"/api/v1/notifications/{result.Value!.Id}", result.Value)
              : Results.BadRequest(result.Error);
    });

    group.MapPost("/{id:guid}/read", async (IUserContext currentUser, Guid id, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var userId = currentUser.UserId;
      var result = await mediator.Send(new MarkNotificationAsReadCommand(tenantId, id, userId));
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    group.MapPatch("/{id:guid}/read", async (IUserContext currentUser, Guid id, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var userId = currentUser.UserId;
      var result = await mediator.Send(new MarkNotificationAsReadCommand(tenantId, id, userId));
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    group.MapPost("/read-all", async (IUserContext currentUser, NotificationsDbContext dbContext, TimeProvider timeProvider) =>
    {
      var tenantId = currentUser.TenantId;
      var userId = currentUser.UserId;
      
      var notifs = await dbContext.Notifications
          .Where(n => n.TenantId == tenantId && n.RecipientUserId == userId && n.StatusValue != "Read" && !n.IsDeleted)
          .ToListAsync();

      foreach (var n in notifs)
      {
          n.MarkAsRead(timeProvider.GetUtcNow().UtcDateTime);
      }

      await dbContext.SaveChangesAsync();
      return Results.Ok();
    });

    group.MapDelete("/{id:guid}", async (IUserContext currentUser, Guid id, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var userId = currentUser.UserId;
      var command = new DeleteNotificationCommand(tenantId, id, userId);
      var result = await mediator.Send(command);
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    // Las preferencias de avisos.
    //
    // Antes esto era mentira en los dos sentidos: el GET devolvía un objeto con valores fijos
    // escritos aquí mismo, y el PUT respondía con el cuerpo que había recibido sin guardar
    // nada. La pantalla funcionaba —los interruptores se movían, el aviso decía «guardado»— y
    // al recargar todo volvía a su sitio. Prometer y no cumplir es peor que no ofrecerlo.
    group.MapGet("/preferences", async (IUserContext currentUser, IMediator mediator) =>
    {
      var result = await mediator.Send(new GetNotificationPreferencesQuery(currentUser.TenantId, currentUser.UserId, currentUser.Role == "Admin"));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    // El cuerpo se enlaza a un tipo, no a `object`. Con `object` cualquier JSON valía y se
    // devolvía tal cual: no había forma de que una hora mal escrita diera error.
    group.MapPut("/preferences", async (NotificationPreferencesRequest body, IUserContext currentUser, IMediator mediator) =>
    {
      // El rol sale del token: es lo que decide si se pueden tocar los avisos de administración.
      var result = await mediator.Send(new SetNotificationPreferencesCommand(
          currentUser.TenantId, currentUser.UserId, currentUser.Role == "Admin",
          body.EmailEnabled, body.PushEnabled,
          body.QuietHoursEnabled, body.QuietHoursStart, body.QuietHoursEnd,
          body.Types ?? []));

      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    return app;
  }
}

/// <summary>
/// El cuerpo del PUT de preferencias: las vías, las horas de silencio y los tipos que se cambian.
/// Los tipos que no vienen se quedan como estaban.
/// </summary>
public sealed record NotificationPreferencesRequest(
    bool EmailEnabled = true,
    bool PushEnabled = false,
    bool QuietHoursEnabled = false,
    string QuietHoursStart = "22:00",
    string QuietHoursEnd = "08:00",
    IReadOnlyList<Notifications.Application.Preferences.NotificationTypeSetting>? Types = null);
