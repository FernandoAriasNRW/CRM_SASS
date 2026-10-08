using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Behaviors;
using MediatR;
using Notifications.Application.Sending;
using Notifications.Domain.Entities;
using Projects.Application.Commands;
using Ticketing.Application.Commands;
using WorkItems.Application.Commands;
using WorkItems.Domain.Entities;
using EntityTypes = BuildingBlocks.Domain.EntityTypes;

namespace ApiHost.Notifications;

/// <summary>
/// Avisa a quien le interesa de lo que pasa en tareas, tickets y proyectos.
///
/// <b>Se cuelga de los mismos comandos que los webhooks</b> (<see cref="WebhookEventNotification"/>):
/// cada comando que dispara un evento ya avisa por ahí cuando sale bien, con lo que se pidió y lo
/// que resultó, así que no hay que repartir eventos de dominio a mano en cada módulo. Mira el tipo
/// del comando, no el nombre del evento, para leer sus campos con tipo.
///
/// A quién: <see cref="InterestedParties"/> —quien lo creó y quien lo tiene asignado—, nunca a quien
/// lo acaba de hacer, y siempre con las preferencias de cada uno (eso lo hace
/// <see cref="INotificationSender"/>).
/// </summary>
public sealed class WorkNotifications(
    INotificationSender sender,
    InterestedParties parties,
    IUserContext currentUser) : INotificationHandler<WebhookEventNotification>
{
    public async Task Handle(WebhookEventNotification notification, CancellationToken ct)
    {
        var tenant = notification.TenantId;
        var actor = currentUser.UserId == Guid.Empty ? (Guid?)null : currentUser.UserId;

        switch (notification.Input)
        {
            // ── Tareas ──────────────────────────────────────────────────────────────────────
            case CreateTaskCommand when notification.Result is WorkTask created:
            {
                var people = await parties.TaskAsync(tenant, created.Id, ct);
                if (people is null) return;
                await SendAsync(tenant, NotificationCatalog.TaskAssigned, "Te han asignado una tarea",
                    $"«{people.Title}»", actor, EntityTypes.Task, created.Id, people.Assignees, ct);
                break;
            }

            case AddTaskAssigneeCommand c:
            {
                var people = await parties.TaskAsync(tenant, c.Id, ct);
                if (people is null) return;
                await SendAsync(tenant, NotificationCatalog.TaskAssigned, "Te han asignado una tarea",
                    $"«{people.Title}»", actor, EntityTypes.Task, c.Id, [c.UserId], ct);
                break;
            }

            case MoveTaskCommand c:
                await TaskStatusAsync(tenant, c.Id, c.NewStatus, actor, ct);
                break;

            case PatchTaskCommand c when c.Status is not null:
                await TaskStatusAsync(tenant, c.Id, c.Status, actor, ct);
                break;

            case PatchTaskCommand c:
            {
                var people = await parties.TaskAsync(tenant, c.Id, ct);
                if (people is null) return;
                await SendAsync(tenant, NotificationCatalog.TaskUpdated, "Una tarea ha cambiado",
                    $"«{people.Title}» tiene cambios.", actor, EntityTypes.Task, c.Id, people.Everyone, ct);
                break;
            }

            case DeleteTaskCommand c:
            {
                var people = await parties.TaskAsync(tenant, c.Id, ct);
                if (people is null) return;
                // Sin enlace: la tarea está en la papelera y el enlace llevaría a una ficha vacía.
                await SendAsync(tenant, NotificationCatalog.TaskDeleted, "Se ha borrado una tarea",
                    $"«{people.Title}» está en la papelera.", actor, null, null, people.Everyone, ct);
                break;
            }

            // ── Tickets ─────────────────────────────────────────────────────────────────────
            case AssignTicketCommand c:
            {
                var people = await parties.TicketAsync(tenant, c.TicketId, ct);
                if (people is null) return;
                await SendAsync(tenant, NotificationCatalog.TicketAssigned, "Te han asignado un ticket",
                    $"«{people.Title}»", actor, EntityTypes.Ticket, c.TicketId, [c.AgentId], ct);
                break;
            }

            case ChangeTicketStatusCommand c:
                await TicketStatusAsync(tenant, c.TicketId, c.NewStatus, actor, ct);
                break;

            case CloseTicketCommand c:
                await TicketStatusAsync(tenant, c.TicketId, "Closed", actor, ct);
                break;

            case UpdateTicketCommand c:
            {
                var people = await parties.TicketAsync(tenant, c.TicketId, ct);
                if (people is null) return;

                if (c.AssignedAgentId is { } agent && agent != Guid.Empty)
                    await SendAsync(tenant, NotificationCatalog.TicketAssigned, "Te han asignado un ticket",
                        $"«{people.Title}»", actor, EntityTypes.Ticket, c.TicketId, [agent], ct);

                if (c.Status is not null)
                    await TicketStatusAsync(tenant, c.TicketId, c.Status, actor, ct);
                else if (c.Title is not null || c.Description is not null || c.Priority is not null)
                    await SendAsync(tenant, NotificationCatalog.TicketUpdated, "Un ticket ha cambiado",
                        $"«{people.Title}» tiene cambios.", actor, EntityTypes.Ticket, c.TicketId, people.Everyone, ct);
                break;
            }

            // ── Proyectos ───────────────────────────────────────────────────────────────────
            case PatchProjectCommand c:
            {
                var people = await parties.ProjectAsync(tenant, c.Id, ct);
                if (people is null) return;
                await SendAsync(tenant, NotificationCatalog.ProjectUpdated, "Un proyecto tuyo ha cambiado",
                    $"«{people.Title}» tiene cambios.", actor, EntityTypes.Project, c.Id, people.Everyone, ct);
                break;
            }

            case DeleteProjectCommand c:
            {
                var people = await parties.ProjectAsync(tenant, c.Id, ct);
                if (people is null) return;
                await SendAsync(tenant, NotificationCatalog.ProjectDeleted, "Se ha borrado un proyecto tuyo",
                    $"«{people.Title}» está en la papelera.", actor, null, null, people.Everyone, ct);
                break;
            }
        }
    }

    /// <summary>
    /// Una tarea cambió de estado. Si quedó terminada es otro aviso —«completada»—, que se puede
    /// tener encendido aunque los cambios de estado intermedios se apaguen.
    /// </summary>
    private async Task TaskStatusAsync(Guid tenant, Guid taskId, string status, Guid? actor, CancellationToken ct)
    {
        var people = await parties.TaskAsync(tenant, taskId, ct);
        if (people is null) return;

        if (status is "Done")
            await SendAsync(tenant, NotificationCatalog.TaskCompleted, "Se ha completado una tarea",
                $"«{people.Title}» está hecha.", actor, EntityTypes.Task, taskId, people.Everyone, ct);
        else
            await SendAsync(tenant, NotificationCatalog.TaskStatusChanged, "Una tarea ha cambiado de estado",
                $"«{people.Title}» ahora está en «{TaskStatusLabel(status)}».", actor, EntityTypes.Task, taskId, people.Everyone, ct);
    }

    private async Task TicketStatusAsync(Guid tenant, Guid ticketId, string status, Guid? actor, CancellationToken ct)
    {
        var people = await parties.TicketAsync(tenant, ticketId, ct);
        if (people is null) return;

        await SendAsync(tenant, NotificationCatalog.TicketStatusChanged, "Un ticket ha cambiado de estado",
            $"«{people.Title}» ahora está en «{TicketStatusLabel(status)}».", actor, EntityTypes.Ticket, ticketId, people.Everyone, ct);
    }

    private Task SendAsync(Guid tenant, string kind, string subject, string body, Guid? actor,
        string? entityType, Guid? entityId, IEnumerable<Guid> recipients, CancellationToken ct)
        => sender.SendAsync(new NotificationMessage(tenant, kind, subject, body, actor, entityType, entityId), recipients, ct);

    /// <summary>Los estados como se leen en la pantalla. Un aviso no debería decir «In Progress».</summary>
    private static string TaskStatusLabel(string status) => status switch
    {
        "To Do" => "Por hacer",
        "In Progress" => "En progreso",
        "In Review" => "En revisión",
        "Done" => "Completado",
        _ => status,
    };

    private static string TicketStatusLabel(string status) => status switch
    {
        "Open" => "Abierto",
        "InProgress" => "En progreso",
        "PendingInfo" => "Pendiente de información",
        "Resolved" => "Resuelto",
        "Closed" => "Cerrado",
        _ => status,
    };
}
