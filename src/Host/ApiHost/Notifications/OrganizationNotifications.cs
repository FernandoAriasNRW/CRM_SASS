using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Application.Events;
using Communication.Application.Commands;
using Communication.Infrastructure.Persistence;
using Identity.Application.Commands;
using Identity.Application.DTOs;
using Identity.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Notifications.Application.Sending;
using Notifications.Domain.Entities;
using Teams.Domain.Events;
using Webhook.Application.Abstractions;

namespace ApiHost.Notifications;

/// <summary>
/// Los avisos que no son de una tarea, un ticket o un proyecto: el chat, los equipos, las cuentas
/// de la organización y los webhooks que dejan de llegar.
///
/// <list type="bullet">
/// <item><b>Chat:</b> a quien ya ha escrito en la conversación. Los canales no tienen lista de
/// miembros; quien ha participado es quien sigue la conversación, y avisar a toda la organización
/// por cada mensaje sería ruido.</item>
/// <item><b>Equipos:</b> a quien entra en un equipo o sale de él.</item>
/// <item><b>Cuentas y webhooks:</b> a quien administra. Son avisos de administración: quien no
/// administra no los recibe ni los puede encender (<see cref="NotificationCatalog"/>).</item>
/// </list>
/// </summary>
public sealed class OrganizationNotifications(
    INotificationSender sender,
    IUserDirectory users,
    CommunicationsDbContext chat,
    IdentityDbContext identity,
    IUserContext currentUser)
    : INotificationHandler<WebhookEventNotification>,
      INotificationHandler<DomainEventNotification<TeamMembersChangedEvent>>,
      INotificationHandler<WebhookDeliveryFailedNotification>
{
    /// <summary>Lo que cabe de un mensaje en el cuerpo de un aviso.</summary>
    private const int ExcerptLength = 160;

    public async Task Handle(WebhookEventNotification notification, CancellationToken ct)
    {
        var tenant = notification.TenantId;
        var actor = currentUser.UserId == Guid.Empty ? (Guid?)null : currentUser.UserId;

        switch (notification.Input)
        {
            case SendMessageCommand c:
            {
                var conversation = await chat.Conversations.IgnoreQueryFilters().AsNoTracking()
                    .Where(x => x.TenantId == tenant && x.Id == c.ConversationId)
                    .Select(x => x.Name)
                    .FirstOrDefaultAsync(ct);
                if (conversation is null) return;

                var participants = await chat.Messages.IgnoreQueryFilters().AsNoTracking()
                    .Where(m => m.TenantId == tenant && m.ConversationId == c.ConversationId && !m.IsDeleted)
                    .Select(m => m.SenderId)
                    .Distinct()
                    .ToListAsync(ct);

                await sender.SendAsync(new NotificationMessage(
                    tenant, NotificationCatalog.ChatMessage,
                    $"Nuevo mensaje en «{conversation}»",
                    Excerpt(c.Content), c.SenderId, "Conversation", c.ConversationId), participants, ct);
                break;
            }

            case CreateUserCommand when notification.Result is UserDto created:
                await ToAdminsAsync(tenant, NotificationCatalog.UserCreated, "Hay una cuenta nueva",
                    $"{created.Name} ({created.Email}) ya puede entrar.", actor, ct);
                break;

            case UpdateUserCommand when notification.Result is UserDto updated:
                await ToAdminsAsync(tenant, NotificationCatalog.UserUpdated, "Ha cambiado una cuenta",
                    $"{updated.Name} ({updated.Email}) tiene cambios.", actor, ct);
                break;

            case DeleteUserCommand c:
            {
                // La cuenta ya está borrada: se busca saltándose el filtro para poder nombrarla.
                var name = await identity.User.IgnoreQueryFilters().AsNoTracking()
                    .Where(u => u.TenantId == tenant && u.Id == c.UserId)
                    .Select(u => u.Name)
                    .FirstOrDefaultAsync(ct);
                await ToAdminsAsync(tenant, NotificationCatalog.UserDeleted, "Se ha borrado una cuenta",
                    $"{name ?? "Una persona"} ya no puede entrar.", actor, ct);
                break;
            }
        }
    }

    public async Task Handle(DomainEventNotification<TeamMembersChangedEvent> notification, CancellationToken ct)
    {
        var e = notification.DomainEvent;
        var actor = currentUser.UserId == Guid.Empty ? (Guid?)null : currentUser.UserId;

        await sender.SendAsync(new NotificationMessage(
            e.TenantId, NotificationCatalog.TeamMemberAdded, "Te han añadido a un equipo",
            $"Ahora estás en «{e.Name}».", actor, "Team", e.TeamId), e.Added, ct);

        // Sin enlace: quien sale ya no tiene por qué ver el tablero del equipo.
        await sender.SendAsync(new NotificationMessage(
            e.TenantId, NotificationCatalog.TeamMemberRemoved, "Ya no estás en un equipo",
            $"Has salido de «{e.Name}».", actor), e.Removed, ct);
    }

    public Task Handle(WebhookDeliveryFailedNotification notification, CancellationToken ct)
        => ToAdminsAsync(notification.TenantId, NotificationCatalog.WebhookDeliveryFailed,
            "Un webhook no se pudo entregar",
            $"«{notification.SubscriptionName}» no recibió «{notification.EventName}» tras varios intentos: {notification.Error}",
            null, ct);

    private async Task ToAdminsAsync(Guid tenant, string kind, string subject, string body, Guid? actor, CancellationToken ct)
    {
        var admins = await users.GetAdminIdsAsync(tenant, ct);
        await sender.SendAsync(new NotificationMessage(tenant, kind, subject, body, actor), admins, ct);
    }

    private static string Excerpt(string text)
        => text.Length <= ExcerptLength ? text : text[..ExcerptLength].TrimEnd() + "…";
}
