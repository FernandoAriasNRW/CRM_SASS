using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using WorkItems.Application.Abstractions;

namespace WorkItems.Infrastructure.Persistence;

/// <summary>
/// Ata el UnitOfWork del módulo WorkItems a su propio <c>DbContext</c>.
/// </summary>
public sealed class WorkItemsModuleUnitOfWork(WorkItemsDbContext context, IOutboxService outboxService)
    : UnitOfWork<WorkItemsDbContext>(context, outboxService), IWorkItemsUnitOfWork
{
}
