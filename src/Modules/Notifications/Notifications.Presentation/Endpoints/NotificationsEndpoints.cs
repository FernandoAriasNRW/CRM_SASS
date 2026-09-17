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
using Notifications.Application.Preferencias;
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

    group.MapGet("", async (IUserContext currentUser, Guid? recipientId, string? type, string? status, IMediator mediator, int page = 1, int pageSize = 25) =>
    {
      var tenantId = currentUser.TenantId;
      var query = new GetNotificationsQuery(tenantId, recipientId, type, status, new() { Page = page, PageSize = pageSize });
      var result = await mediator.Send(query);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/{id:guid}", async (IUserContext currentUser, Guid id, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var query = new GetNotificationByIdQuery(tenantId, id);
      var result = await mediator.Send(query);
      return result.Value is null ? Results.NotFound() : Results.Ok(result.Value);
    });

    group.MapGet("/unread-count", async (IUserContext currentUser, Guid recipientId, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var result = await mediator.Send(new GetUnreadCountQuery(tenantId, recipientId));
      return Results.Ok(new { Count = result });
    });

    // Inquilino y remitente, del token. Ver ProjectsEndpoints: misma grieta.
    //
    // Aquí el destinatario sí llega en el cuerpo, y debe seguir así: notificar a otra persona
    // es justo lo que hace este endpoint. Lo que no puede elegir quien llama es *en qué
    // organización* deja la notificación, ni firmarla con el nombre de otro.
    group.MapPost("", async (CreateNotificationCommand command, IUserContext usuario, IMediator mediator) =>
    {
      var result = await mediator.Send(command with
      {
        TenantId = usuario.TenantId,
        SenderUserId = usuario.UserId,
      });

      return result.IsSuccess
              ? Results.Created($"/api/v1/notifications/{result.Value!.Id}", result.Value)
              : Results.BadRequest(result.Error);
    });

    group.MapPost("/{id:guid}/read", async (IUserContext currentUser, Guid id, Guid? recipientId, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var userId = recipientId ?? (currentUser.UserId);
      var result = await mediator.Send(new MarkNotificationAsReadCommand(tenantId, id, userId));
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    group.MapPatch("/{id:guid}/read", async (IUserContext currentUser, Guid id, Guid? recipientId, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var userId = recipientId ?? (currentUser.UserId);
      var result = await mediator.Send(new MarkNotificationAsReadCommand(tenantId, id, userId));
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    group.MapPost("/read-all", async (IUserContext currentUser, NotificationsDbContext dbContext) =>
    {
      var tenantId = currentUser.TenantId;
      var userId = currentUser.UserId;
      
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
    group.MapGet("/preferences", async (IUserContext usuario, IMediator mediator) =>
    {
      var result = await mediator.Send(new GetPreferenciasQuery(usuario.TenantId, usuario.UserId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    // El cuerpo se enlaza a un tipo, no a `object`. Con `object` cualquier JSON valía y se
    // devolvía tal cual: no había forma de que una hora mal escrita diera error.
    group.MapPut("/preferences", async (PreferenciasRequest cuerpo, IUserContext usuario, IMediator mediator) =>
    {
      var result = await mediator.Send(new SetPreferenciasCommand(
          usuario.TenantId, usuario.UserId,
          cuerpo.EmailEnabled, cuerpo.PushEnabled,
          cuerpo.TaskAssigned, cuerpo.TaskCompleted, cuerpo.TaskDueSoon,
          cuerpo.TicketCreated, cuerpo.TicketUpdated, cuerpo.ProjectUpdated,
          cuerpo.MentionEnabled, cuerpo.ExportReady,
          cuerpo.QuietHoursEnabled, cuerpo.QuietHoursStart, cuerpo.QuietHoursEnd));

      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    return app;
  }
}

/// <summary>
/// El cuerpo del PUT de preferencias. Los nombres son los que ya mandaba la pantalla.
///
/// Cada campo trae el valor de por defecto que le corresponde: si una versión antigua de la
/// interfaz manda un JSON sin `exportReady`, la preferencia queda encendida —que es lo que
/// espera quien no la ha tocado— en vez de apagarse sola por omisión.
/// </summary>
public sealed record PreferenciasRequest(
    bool EmailEnabled = true,
    bool PushEnabled = false,
    bool TaskAssigned = true,
    bool TaskCompleted = false,
    bool TaskDueSoon = true,
    bool TicketCreated = true,
    bool TicketUpdated = false,
    bool ProjectUpdated = true,
    bool MentionEnabled = true,
    bool ExportReady = true,
    bool QuietHoursEnabled = false,
    string QuietHoursStart = "22:00",
    string QuietHoursEnd = "08:00");
