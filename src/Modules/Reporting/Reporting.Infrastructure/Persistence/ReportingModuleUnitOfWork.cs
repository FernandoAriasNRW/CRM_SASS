using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure.DomainEvents;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Reporting.Application.Abstractions;

namespace Reporting.Infrastructure.Persistence;

/// <summary>
/// Ata el UnitOfWork del módulo Reporting a su propio <c>DbContext</c>.
/// </summary>
public sealed class ReportingModuleUnitOfWork(
    ReportingDbContext context,
    IOutboxService outboxService,
    // El despachador es opcional en la clase base, y no pasarlo hacía que
    // `SaveChangesAndDispatchAsync` guardara sin repartir nada: un método cuyo nombre promete
    // algo que no hacía. Se pide como dependencia obligatoria para que el hueco no pueda volver.
    IDomainEventDispatcher domainEventDispatcher)
    : UnitOfWork<ReportingDbContext>(context, outboxService, domainEventDispatcher), IReportingUnitOfWork
{
}
