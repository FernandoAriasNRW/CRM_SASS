using Automations.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Notifications.Application.Commands;
using Notifications.Application.Preferences;
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
    TimeProvider timeProvider,
    IMediator mediator,
    WorkItemsDbContext tasks,
    INotificationPreferencesRepository preferences)
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

        // Las preferencias mandan. Se usa la propia función del dominio de Notifications en vez
        // de repetir la lógica aquí: son las mismas reglas, incluidas las horas de silencio y su
        // cruce de medianoche.
        var theirs = await preferences.GetForUserAsync(tenantId, recipientId, ct)
                    ?? NotificationPreferences.CreateDefault(tenantId, recipientId);

        var now = TimeOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        if (!theirs.ShouldDeliver(NotificationTypes.TaskDueSoon, now))
        {
            // No es un fallo: es la persona ejerciendo su preferencia. Se devuelve sin más para
            // que el motor lo cuente como aplicado —la regla hizo lo que tenía que hacer— en vez
            // de como error, que llenaría el registro de falsos problemas.
            return;
        }

        var result = await mediator.Send(new CreateNotificationCommand(
            TenantId: tenantId,
            RecipientUserId: recipientId,
            Type: "InApp",
            Subject: "Una tarea necesita tu atención",
            Body: $"«{task.Title}» se acerca a su fecha de vencimiento.",
            // Sin remitente: no lo manda una persona, lo manda una regla que alguien configuró
            // antes. Poner aquí a quien tocó la tarea le atribuiría un aviso que no escribió.
            SenderUserId: null), ct);

        if (!result.IsSuccess)
            throw new InvalidOperationException(result.Error);
    }
}
