using BuildingBlocks.Application.Events;
using MediatR;
using Notifications.Application.Commands;
using Notifications.Application.Preferences;
using Notifications.Domain.Entities;
using Reporting.Domain.Events;

namespace ApiHost.Reporting;

internal static class ExportNotifications
{
    public static async Task NotifyAsync(
        IMediator mediator,
        INotificationPreferencesRepository preferences,
        ILogger logger,
        Guid tenantId,
        Guid recipient,
        string subject,
        string body,
        CancellationToken ct)
    {
        if (recipient == Guid.Empty) return;

        // Las preferencias mandan, igual que en las automatizaciones. Se usa la propia función
        // del dominio de Notifications en vez de repetir aquí las reglas —incluidas las horas de
        // silencio y su cruce de medianoche—: son las mismas reglas y no pueden divergir.
        var theirs = await preferences.GetForUserAsync(tenantId, recipient, ct)
                    ?? NotificationPreferences.CreateDefault(tenantId, recipient);

        var now = TimeOnly.FromDateTime(DateTime.UtcNow);

        if (!theirs.ShouldDeliver(NotificationTypes.ExportReady, now))
        {
            // No es un fallo: es la persona ejerciendo la preferencia que la pantalla le ofrece.
            // La exportación sigue estando en su lista, así que apagar el aviso no esconde el
            // fichero, sólo deja de interrumpir.
            return;
        }

        var result = await mediator.Send(new CreateNotificationCommand(
            TenantId: tenantId,
            RecipientUserId: recipient,
            Type: "InApp",
            Subject: subject,
            Body: body,
            // Sin remitente: no lo manda una persona, lo manda el propio sistema al terminar un
            // trabajo que esa misma persona pidió.
            SenderUserId: null), ct);

        if (!result.IsSuccess)
        {
            // Que el aviso no salga no puede tumbar la exportación: el fichero ya está generado y
            // se descarga igual desde la pantalla. Se anota y se sigue.
            logger.LogWarning(
                "No se pudo avisar a {Persona} de su exportación: {Error}", recipient, result.Error);
        }
    }
}
