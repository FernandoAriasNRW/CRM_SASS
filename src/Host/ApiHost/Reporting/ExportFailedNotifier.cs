using BuildingBlocks.Application.Events;
using MediatR;
using Notifications.Application.Commands;
using Notifications.Application.Preferences;
using Notifications.Domain.Entities;
using Reporting.Domain.Events;

namespace ApiHost.Reporting;

public sealed class ExportFailedNotifier(
    IMediator mediator,
    INotificationPreferencesRepository preferences,
    ILogger<ExportFailedNotifier> logger)
    : INotificationHandler<DomainEventNotification<ExportFailedEvent>>
{
    public async Task Handle(DomainEventNotification<ExportFailedEvent> notification, CancellationToken ct)
    {
        var domainEvent = notification.DomainEvent;

        await ExportNotifications.NotifyAsync(
            mediator, preferences, logger,
            domainEvent.TenantId, domainEvent.RequestedById,
            "Tu exportación no salió",
            // El motivo va en el aviso, no sólo en la pantalla de exportaciones. Quien recibe
            // «falló» a secas tiene que ir a buscar el porqué; quien recibe el porqué a veces
            // puede arreglarlo solo.
            $"No se pudo generar el informe: {domainEvent.Error}",
            ct);
    }
}
