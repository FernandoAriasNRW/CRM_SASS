using Automations.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Notifications.Application.Sending;
using Notifications.Domain.Entities;
using WorkItems.Infrastructure.Persistence;

namespace ApiHost.Services;

/// <summary>
/// Manda el aviso de una automatización a la persona que toca.
///
/// **Vive en el host porque cruza tres módulos**: Automations decide que hay que avisar,
/// WorkItems sabe quién tiene la tarea y Notifications sabe entregar el aviso. Ninguno de los
/// tres referencia a los otros; el host los conoce a todos y es donde se compone.
///
/// Dos decisiones que importan más que el código:
///
/// **Se respetan las preferencias de quien recibe.** Quien apagó un tipo de aviso no empieza a
/// recibirlo porque alguien configure una automatización. Automatizar no es un permiso para
/// saltarse lo que la persona ya decidió, y si lo fuera, la pantalla de preferencias sería otra
/// promesa que no se cumple.
///
/// **Sin destinatario no se avisa, y se dice.** Una regla que avisa «al responsable» sobre una
/// tarea sin asignar no tiene a quién avisar. Se lanza para que el motor lo anote como fallo en
/// el registro de ejecuciones: es exactamente lo que hay que poder leer cuando alguien pregunta
/// por qué no le llegó nada.
/// </summary>
public sealed class AutomationNotifier(
    WorkItemsDbContext tasks,
    INotificationSender sender)
{
    public async Task NotifyAsync(Guid tenantId, Guid taskId, string recipient, CancellationToken ct)
    {
        var task = await tasks.Tasks
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.Id == taskId)
            .Select(t => new { t.AssigneeId, Title = t.Title.Value })
            .FirstOrDefaultAsync(ct);

        if (task is null)
            throw new InvalidOperationException("La tarea ya no existe");

        var recipientId = recipient == ActionTypes.AssigneeRecipient
            ? task.AssigneeId
            : Guid.TryParse(recipient, out var id) ? id : Guid.Empty;

        if (recipientId == Guid.Empty)
        {
            throw new InvalidOperationException(
                recipient == ActionTypes.AssigneeRecipient
                    ? "La tarea no tiene responsable a quien avisar"
                    : $"«{recipient}» no es un destinatario válido");
        }

        // Por el remitente común: aplica las preferencias —incluidas las horas de silencio— igual
        // que a cualquier otro aviso. Que la persona lo tenga apagado no es un fallo de la regla:
        // la regla hizo lo que tenía que hacer, y por eso no se lanza nada si no llega.
        //
        // Sin quien lo cause: no lo manda una persona, lo manda una regla que alguien configuró
        // antes. Atribuírselo a quien tocó la tarea le pondría un aviso que no escribió.
        await sender.SendAsync(new NotificationMessage(
            tenantId, NotificationCatalog.TaskDueSoon,
            "Una tarea necesita tu atención",
            $"«{task.Title}» se acerca a su fecha de vencimiento.",
            EntityType: BuildingBlocks.Domain.EntityTypes.Task, EntityId: taskId), [recipientId], ct);
    }
}
