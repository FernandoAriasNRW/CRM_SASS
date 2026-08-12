using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Identity.Application.Abstractions;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Ata el UnitOfWork del módulo Identity a su propio <c>DbContext</c>.
/// </summary>
public sealed class IdentityModuleUnitOfWork(IdentityDbContext context, IOutboxService outboxService)
    : UnitOfWork<IdentityDbContext>(context, outboxService), IIdentityUnitOfWork
{
}
