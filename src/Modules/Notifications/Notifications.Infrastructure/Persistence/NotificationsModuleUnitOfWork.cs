using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Notifications.Application.Abstractions;

namespace Notifications.Infrastructure.Persistence;

/// <summary>
/// Ata el UnitOfWork del módulo Notifications a su propio <c>DbContext</c>.
/// </summary>
public sealed class NotificationsModuleUnitOfWork(NotificationsDbContext context, IOutboxService outboxService, TimeProvider timeProvider)
    : UnitOfWork<NotificationsDbContext>(context, outboxService, timeProvider), INotificationsUnitOfWork
{
}
