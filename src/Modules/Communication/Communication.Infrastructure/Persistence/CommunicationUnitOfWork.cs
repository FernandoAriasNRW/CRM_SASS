using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace Communication.Infrastructure.Persistence;

public sealed class CommunicationUnitOfWork(CommunicationsDbContext context) : IUnitOfWork<CommunicationsDbContext>
{
  private readonly CommunicationsDbContext _context = context;
  private IDbContextTransaction? _transaction;

  public async Task BeginTransactionAsync(CancellationToken ct = default)
  {
    if (_transaction is not null)
      return;

    _transaction = await _context.Database.BeginTransactionAsync(ct);
  }

  public async Task CommitTransactionAsync(CancellationToken ct = default)
  {
    if (_transaction is null)
      return;

    await _transaction.CommitAsync(ct);
    await _transaction.DisposeAsync();
    _transaction = null;
  }

  public async Task RollbackTransactionAsync(CancellationToken ct = default)
  {
    if (_transaction is null)
      return;

    await _transaction.RollbackAsync(ct);
    await _transaction.DisposeAsync();
    _transaction = null;
  }

  public async Task<int> SaveChangesAsync(CancellationToken ct = default)
  {
    return await _context.SaveChangesAsync(ct);
  }
}
