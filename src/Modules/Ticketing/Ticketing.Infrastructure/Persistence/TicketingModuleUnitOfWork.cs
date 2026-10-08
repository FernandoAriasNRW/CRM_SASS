using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure.DomainEvents;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Ticketing.Application.Abstractions;

namespace Ticketing.Infrastructure.Persistence;

/// <summary>
/// Ata el UnitOfWork del módulo Ticketing a su propio <c>DbContext</c>.
///
/// Pide el repartidor de eventos, igual que WorkItems: sin él, la clase base guarda y no reparte
/// nada en proceso, en silencio.
/// </summary>
public sealed class TicketingModuleUnitOfWork(TicketingDbContext context, IOutboxService outboxService, TimeProvider timeProvider,
    IDomainEventDispatcher domainEventDispatcher)
    : UnitOfWork<TicketingDbContext>(context, outboxService, timeProvider, domainEventDispatcher), ITicketingUnitOfWork
{
}
