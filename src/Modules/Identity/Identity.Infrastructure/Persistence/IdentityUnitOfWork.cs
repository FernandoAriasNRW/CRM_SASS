using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace Identity.Infrastructure.Persistence;

public sealed class IdentityUnitOfWork(IdentityDbContext context) : IUnitOfWork<IdentityDbContext>
{
  private readonly IdentityDbContext _context = context;
  private IDbContextTransaction? _transaction;

  public async Task? BeginTransactionAsync(CancellationToken ct = default)
  {
    if (_transaction is not null)
      return;

    _transaction = await _context.Database.BeginTransactionAsync(ct);
  }

  public async Task CommitTransactionAsync(CancellationToken ct = default)
  {
    try
    {
      // Guarda cambios pendientes
      await _context.SaveChangesAsync(ct);

      if (_transaction is not null)
      {
        await _transaction.CommitAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
      }
    }
    catch
    {
      await RollbackTransactionAsync(ct);
      throw;
    }
  }

  public async Task RollbackTransactionAsync(CancellationToken ct = default)
  {
    if (_transaction is not null)
    {
      await _transaction.RollbackAsync(ct);
      await _transaction.DisposeAsync();
      _transaction = null;
    }
  }

  public async Task<int> SaveChangesAsync(CancellationToken ct = default)
  {
    return await _context.SaveChangesAsync(ct);
  }
}