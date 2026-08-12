using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Communication.Application.Abstractions;

namespace Communication.Infrastructure.Persistence;

/// <summary>
/// Ata el UnitOfWork del módulo Communication a su propio <c>DbContext</c>.
/// </summary>
public sealed class CommunicationModuleUnitOfWork(CommunicationsDbContext context, IOutboxService outboxService)
    : UnitOfWork<CommunicationsDbContext>(context, outboxService), ICommunicationUnitOfWork
{
}
