using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Reporting.Application.Abstractions;

namespace Reporting.Infrastructure.Persistence;

/// <summary>
/// Ata el UnitOfWork del módulo Reporting a su propio <c>DbContext</c>.
/// </summary>
public sealed class ReportingModuleUnitOfWork(ReportingDbContext context, IOutboxService outboxService)
    : UnitOfWork<ReportingDbContext>(context, outboxService), IReportingUnitOfWork
{
}
