using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;
using Ticketing.Infrastructure.Persistence;
using Ticketing.Application.Abstractions.Queries;
using Ticketing.Application.DTOs;

namespace Ticketing.Infrastructure.Queries;

public sealed class TicketQueries(TicketingDbContext context) : ITicketQueries
{
    public async Task<PagedResult<TicketDto>> GetByTenantAsync(
        Guid tenantId, Guid? customerId, Guid? agentId, string? priority, string? status,
        int page, int pageSize, CancellationToken ct = default)
    {
        return await GetByTenantWithPaginationAsync(
            tenantId, customerId, agentId, priority, status,
            new PaginationRequest { Page = page, PageSize = pageSize },
            ct: ct);
    }

    public async Task<PagedResult<TicketDto>> GetByTenantWithPaginationAsync(
        Guid tenantId, Guid? customerId, Guid? agentId, string? priority, string? status,
        PaginationRequest pagination,
        string? filter = null, Guid? userId = null, IReadOnlyList<Guid>? idsFavoritos = null,
        CancellationToken ct = default)
    {
        var query = context.Tickets.AsNoTracking().Where(t => t.TenantId == tenantId);

        // Los filtros del panel de navegación.
        //
        // Esto no existía, y el menú ya ofrecía «Mis Tickets» apuntando a `?filter=mine`: el
        // parámetro llegaba, nadie lo leía y la pantalla devolvía los 175 tickets de siempre.
        // Quien lo usaba creía estar viendo los suyos.
        if (!string.IsNullOrWhiteSpace(filter))
        {
            if (FiltrosDeVista.Es(filter, FiltrosDeVista.Mios) && userId.HasValue)
            {
                // «Míos» en un ticket es el agente que lo lleva, no quien lo abrió: el que abre
                // suele ser un cliente y no tiene esta pantalla.
                query = query.Where(t => t.AssignedAgentId == userId.Value);
            }
            else if (FiltrosDeVista.Es(filter, FiltrosDeVista.CreadosPorMi) && userId.HasValue)
            {
                query = query.Where(t => t.CustomerId == userId.Value);
            }
            else if (FiltrosDeVista.Es(filter, FiltrosDeVista.Favoritos))
            {
                // Sin marcados, la lista es vacía y no «todos». Devolver todo cuando no hay
                // favoritos sería el mismo engaño que se está arreglando, sólo que al revés.
                var marcados = (idsFavoritos ?? []).ToArray();

                // `EF.Constant` incrusta los identificadores en el SQL en vez de pasarlos como
                // un parámetro de colección. Hace falta: el proveedor de MySQL no traduce una
                // colección parametrizada y la consulta fallaba con «Primitive collections
                // support has not been enabled» —un 409 en la cara de quien pulsara «Favoritos»—.
                //
                // Es seguro porque la lista está acotada: Favorito.MaximoPorPersonaYTipo son 200
                // identificadores como mucho. Sin ese tope, incrustar sería un problema distinto.
                query = query.Where(t => EF.Constant(marcados).Contains(t.Id));
            }
        }

        if (customerId.HasValue) query = query.Where(t => t.CustomerId == customerId.Value);
        if (agentId.HasValue) query = query.Where(t => t.AssignedAgentId == agentId.Value);
        if (!string.IsNullOrEmpty(priority))
        {
            var pVal = priority switch { "Low" => 1, "Medium" => 2, "High" => 3, "Urgent" => 4, _ => 0 };
            if (pVal > 0) query = query.Where(t => t.PriorityValue == pVal);
        }
        if (!string.IsNullOrEmpty(status))
        {
            var sVal = status switch { "Open" => 1, "InProgress" => 2, "PendingInfo" => 3, "Resolved" => 4, "Closed" => 5, _ => 0 };
            if (sVal > 0) query = query.Where(t => t.StatusValue == sVal);
        }

        if (pagination.StartDate.HasValue) query = query.Where(t => t.CreatedAt >= pagination.StartDate.Value);
        if (pagination.EndDate.HasValue) query = query.Where(t => t.CreatedAt <= pagination.EndDate.Value);

        var limitDate = DateTime.UtcNow.AddMonths(-3);
        query = query.Where(t => !((t.StatusValue == 4 || t.StatusValue == 5) && (t.ResolvedAt ?? t.CreatedAt) < limitDate));

        var totalCount = await query.CountAsync(ct);
        
        // Apply Sorting
        var desc = pagination.SortDirection?.ToLower() == "desc";
        query = pagination.SortColumn?.ToLower() switch
        {
            "title" => desc ? query.OrderByDescending(t => t.Title) : query.OrderBy(t => t.Title),
            "status" => desc ? query.OrderByDescending(t => t.StatusValue) : query.OrderBy(t => t.StatusValue),
            "priority" => desc ? query.OrderByDescending(t => t.PriorityValue) : query.OrderBy(t => t.PriorityValue),
            "createdat" => desc ? query.OrderByDescending(t => t.CreatedAt) : query.OrderBy(t => t.CreatedAt),
            _ => query.OrderByDescending(t => t.CreatedAt)
        };

        var items = await query
            .Skip(pagination.Skip).Take(pagination.Take)
            .Select(t => new TicketDto(t.Id, t.TenantId, t.CustomerId, t.AssignedAgentId,
                t.Title, t.Description, 
                t.PriorityValue == 1 ? "Low" : t.PriorityValue == 2 ? "Medium" : t.PriorityValue == 3 ? "High" : t.PriorityValue == 4 ? "Urgent" : "Unknown", 
                t.StatusValue == 1 ? "Open" : t.StatusValue == 2 ? "InProgress" : t.StatusValue == 3 ? "PendingInfo" : t.StatusValue == 4 ? "Resolved" : t.StatusValue == 5 ? "Closed" : "Unknown", 
                t.CreatedAt, t.ResolvedAt))
            .ToListAsync(ct);

        return PagedResult<TicketDto>.Create(items, totalCount, pagination.Page, pagination.PageSize);
    }

    public async Task<TicketDto?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await context.Tickets.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.Id == id)
            .Select(t => new TicketDto(t.Id, t.TenantId, t.CustomerId, t.AssignedAgentId,
                t.Title, t.Description, 
                t.PriorityValue == 1 ? "Low" : t.PriorityValue == 2 ? "Medium" : t.PriorityValue == 3 ? "High" : t.PriorityValue == 4 ? "Urgent" : "Unknown", 
                t.StatusValue == 1 ? "Open" : t.StatusValue == 2 ? "InProgress" : t.StatusValue == 3 ? "PendingInfo" : t.StatusValue == 4 ? "Resolved" : t.StatusValue == 5 ? "Closed" : "Unknown", 
                t.CreatedAt, t.ResolvedAt))
            .FirstOrDefaultAsync(ct);
    }
}
