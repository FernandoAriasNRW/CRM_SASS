using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Projects.Application.Abstractions;

namespace Projects.Infrastructure.Persistence;

/// <summary>
/// Ata el UnitOfWork del módulo Projects a su propio <c>DbContext</c>.
/// </summary>
public sealed class ProjectsModuleUnitOfWork(ProjectsDbContext context, IOutboxService outboxService, TimeProvider timeProvider,
    BuildingBlocks.Infrastructure.DomainEvents.IDomainEventDispatcher domainEventDispatcher)
    : UnitOfWork<ProjectsDbContext>(context, outboxService, timeProvider, domainEventDispatcher), IProjectsUnitOfWork
{
}
