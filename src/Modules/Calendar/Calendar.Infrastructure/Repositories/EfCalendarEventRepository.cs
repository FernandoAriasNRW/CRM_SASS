using Calendar.Application.Abstractions.Repositories;
using Calendar.Domain.Entities;
using Calendar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Calendar.Infrastructure.Repositories;

/// <summary>
/// Implementación del Repository para operaciones de ESCRITURA.
/// Separación CQRS: Este repository SOLO se usa para Commands.
/// Para Queries usar CalendarEventQueries.
/// </summary>
public sealed class EfCalendarEventRepository(CalendarDbContext context) : ICalendarEventRepository
{
    private readonly CalendarDbContext _context = context;

    public async Task AddAsync(CalendarEvent calendarEvent, CancellationToken ct = default)
    {
        await _context.CalendarEvents.AddAsync(calendarEvent, ct);
        // El SaveChanges se maneja en el UnitOfWork
    }

    public async Task UpdateAsync(CalendarEvent calendarEvent, CancellationToken ct = default)
    {
        _context.CalendarEvents.Update(calendarEvent);
        await Task.CompletedTask;
    }

    public async Task<CalendarEvent?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default,
        bool includeDeleted = false)
    {
        var query = _context.CalendarEvents.AsQueryable();

        // Aplicar filtro global de soft delete
        if (!includeDeleted)
        {
            query = query.Where(e => !e.IsDeleted);
        }

        return await query
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == id, ct);
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var evt = await _context.CalendarEvents
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == id, ct);

        if (evt is not null)
        {
            _context.CalendarEvents.Remove(evt);
        }
    }
}