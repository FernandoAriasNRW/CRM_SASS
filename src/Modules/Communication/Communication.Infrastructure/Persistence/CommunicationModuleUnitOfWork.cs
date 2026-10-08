using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure.DomainEvents;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Communication.Application.Abstractions;

namespace Communication.Infrastructure.Persistence;

/// <summary>
/// Ata el UnitOfWork del módulo Communication a su propio <c>DbContext</c>.
///
/// Pide el repartidor de eventos, igual que WorkItems: sin él, la clase base guarda y no reparte
/// nada en proceso, en silencio.
/// </summary>
public sealed class CommunicationModuleUnitOfWork(CommunicationsDbContext context, IOutboxService outboxService, TimeProvider timeProvider,
    IDomainEventDispatcher domainEventDispatcher)
    : UnitOfWork<CommunicationsDbContext>(context, outboxService, timeProvider, domainEventDispatcher), ICommunicationUnitOfWork
{
}
