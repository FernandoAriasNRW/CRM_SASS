using BuildingBlocks.Application.Events;
using MediatR;
using Notifications.Application.Sending;
using Notifications.Domain.Entities;
using Reporting.Domain.Events;

namespace ApiHost.Reporting;

/// <summary>
/// Avisa a quien pidió una exportación de que ya está.
///
/// <b>Vive en el host porque cruza dos módulos</b>: Reporting sabe que la exportación terminó y
/// Notifications sabe entregar el aviso; ninguno referencia al otro. Entrega por
/// <see cref="INotificationSender"/>, como todos los avisos, para que las preferencias se
/// respeten igual en todas partes.
/// </summary>
public sealed class ExportReadyNotifier(INotificationSender sender)
    : INotificationHandler<DomainEventNotification<ExportReadyEvent>>
{
    public Task Handle(DomainEventNotification<ExportReadyEvent> notification, CancellationToken ct)
    {
        var e = notification.DomainEvent;
        return sender.SendAsync(new NotificationMessage(
            e.TenantId, NotificationCatalog.ExportReady,
            "Tu exportación está lista",
            $"«{e.FileName}» ya se puede descargar."), [e.RequestedById], ct);
    }
}

/// <summary>
/// Y de que no salió. Quien pide un informe y no recibe nada no sabe si esperar más: un fallo
/// callado convierte cada exportación en una pregunta al soporte.
/// </summary>
public sealed class ExportFailedNotifier(INotificationSender sender)
    : INotificationHandler<DomainEventNotification<ExportFailedEvent>>
{
    public Task Handle(DomainEventNotification<ExportFailedEvent> notification, CancellationToken ct)
    {
        var e = notification.DomainEvent;
        return sender.SendAsync(new NotificationMessage(
            e.TenantId, NotificationCatalog.ExportFailed,
            "Tu exportación no salió",
            // El motivo va en el aviso: quien recibe el porqué a veces puede arreglarlo solo.
            $"No se pudo generar el informe: {e.Error}"), [e.RequestedById], ct);
    }
}
