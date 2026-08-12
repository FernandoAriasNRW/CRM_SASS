using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Ticketing.Application.Abstractions;

namespace Ticketing.Infrastructure.Persistence;

/// <summary>
/// Ata el UnitOfWork del módulo Ticketing a su propio <c>DbContext</c>.
/// </summary>
public sealed class TicketingModuleUnitOfWork(TicketingDbContext context, IOutboxService outboxService)
    : UnitOfWork<TicketingDbContext>(context, outboxService), ITicketingUnitOfWork
{
}
