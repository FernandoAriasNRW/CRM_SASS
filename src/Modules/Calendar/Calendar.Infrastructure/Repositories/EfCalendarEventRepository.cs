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
        // `IgnoreQueryFilters` cuando se piden los borrados, no un `Where` a mano.
        //
        // **Esto era un parámetro que no hacía nada.** Antes, con `includeDeleted: true` sólo se
        // dejaba de añadir `Where(e => !e.IsDeleted)`, pero el filtro global del `DbContext` sigue
        // ahí y excluye los borrados igualmente: la consulta nunca veía el evento. Restaurar algo
        // de la papelera respondía «Evento no encontrado» **teniéndolo delante**, así que lo que
        // iba a la papelera no salía nunca.
        //
        // Al apagar los filtros se cae también el de inquilino, y por eso el `TenantId` se
        // compara explícitamente abajo: sin esa condición, esto leería eventos de otro cliente.
        var query = includeDeleted
            ? _context.CalendarEvents.IgnoreQueryFilters()
            : _context.CalendarEvents.AsQueryable();

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