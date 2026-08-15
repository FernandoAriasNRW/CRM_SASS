using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure.DomainEvents;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using WorkItems.Application.Abstractions;

namespace WorkItems.Infrastructure.Persistence;

/// <summary>
/// Ata el UnitOfWork del módulo WorkItems a su propio <c>DbContext</c>.
///
/// **El repartidor de eventos hay que pedirlo explícitamente.** En la clase base es un parámetro
/// opcional que por defecto es nulo, y este módulo no lo pasaba: `SaveChangesAndDispatchAsync`
/// guardaba y no repartía nada, en silencio y sin fallar. El resultado era que el aviso al
/// tablero por SignalR llevaba escrito desde siempre sin ejecutarse nunca, y que el motor de
/// automatizaciones no se enteraba de ningún cambio.
/// </summary>
public sealed class WorkItemsModuleUnitOfWork(
    WorkItemsDbContext context,
    IOutboxService outboxService,
    IDomainEventDispatcher domainEventDispatcher)
    : UnitOfWork<WorkItemsDbContext>(context, outboxService, domainEventDispatcher), IWorkItemsUnitOfWork
{
}
