using BuildingBlocks.Application.Events;
using MediatR;
using Notifications.Application.Commands;
using Notifications.Application.Preferences;
using Notifications.Domain.Entities;
using Reporting.Domain.Events;

namespace ApiHost.Reporting;

/// <summary>
/// Avisa a quien pidió una exportación de que ya está —o de que no salió—.
///
/// <b>Vive en el host porque cruza dos módulos</b>: Reporting sabe que la exportación terminó y
/// Notifications sabe entregar el aviso; ninguno referencia al otro. Es el mismo reparto que
/// <see cref="ApiHost.Services.AutomationNotifier"/>, y se sigue igual a propósito: dos formas
/// distintas de avisar acabarían respetando las preferencias de dos maneras distintas.
///
/// <b>Se avisa también del fallo.</b> Es lo que el plan señalaba: quien pide un informe y no
/// recibe nada no sabe si esperar más. Un fallo callado convierte cada exportación en una
/// pregunta al soporte.
/// </summary>
public sealed class ExportReadyNotifier(
    TimeProvider timeProvider,
    IMediator mediator,
    INotificationPreferencesRepository preferences,
    ILogger<ExportReadyNotifier> logger)
    : INotificationHandler<DomainEventNotification<ExportReadyEvent>>
{
    public async Task Handle(DomainEventNotification<ExportReadyEvent> notification, CancellationToken ct)
    {
        var domainEvent = notification.DomainEvent;

        await ExportNotifications.NotifyAsync(
            mediator, preferences, logger,
            domainEvent.TenantId, domainEvent.RequestedById,
            "Tu exportación está lista",
            $"«{domainEvent.FileName}» ya se puede descargar.",
            timeProvider.GetUtcNow().UtcDateTime,
            ct);
    }
}
