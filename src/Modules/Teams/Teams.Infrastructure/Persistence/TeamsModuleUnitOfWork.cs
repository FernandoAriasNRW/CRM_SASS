using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Teams.Application.Abstractions;

namespace Teams.Infrastructure.Persistence;

/// <summary>
/// Ata el UnitOfWork del módulo Teams a su propio <c>DbContext</c>.
/// </summary>
public sealed class TeamsModuleUnitOfWork(TeamsDbContext context, IOutboxService outboxService)
    : UnitOfWork<TeamsDbContext>(context, outboxService), ITeamsUnitOfWork
{
}
