using BuildingBlocks.Infrastructure.Persistence;

namespace Calendar.Infrastructure.Persistence;

/// <summary>
/// Interfaz para el UnitOfWork específico del módulo Calendar.
/// </summary>
public interface ICalendarUnitOfWork : IUnitOfWork<CalendarDbContext>
{
    CalendarDbContext Context { get; }
}

/// <summary>
/// Implementación del UnitOfWork para el módulo Calendar.
/// Maneja transacciones y persistencia de cambios.
/// </summary>
public sealed class CalendarUnitOfWork : ICalendarUnitOfWork
{
    private readonly CalendarDbContext _context;

    public CalendarUnitOfWork(CalendarDbContext context)
    {
        _context = context;
    }

    public CalendarDbContext Context => _context;

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        await _context.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        await _context.Database.CommitTransactionAsync(ct);
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        await _context.Database.RollbackTransactionAsync(ct);
    }
}