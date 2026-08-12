using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Calendar.Application.Abstractions;

namespace Calendar.Infrastructure.Persistence;

/// <summary>
/// Ata el UnitOfWork del módulo Calendar a su propio <c>DbContext</c>.
/// </summary>
public sealed class CalendarModuleUnitOfWork(CalendarDbContext context, IOutboxService outboxService)
    : UnitOfWork<CalendarDbContext>(context, outboxService), Calendar.Application.Abstractions.ICalendarUnitOfWork
{
}
