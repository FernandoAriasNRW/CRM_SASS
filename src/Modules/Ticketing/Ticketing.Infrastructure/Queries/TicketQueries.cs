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
        AlcanceDeVista? alcance = null,
        CancellationToken ct = default)
    {
        var vista = alcance ?? AlcanceDeVista.Ninguno;

        // La papelera y el archivo están fuera de lo que el filtro global deja ver, así que no
        // basta con un `Where`: hay que abrir el alcance antes de construir la consulta. El
        // ámbito se cierra al terminar el método, de modo que ninguna consulta posterior de esta
        // petición hereda la apertura.
        using var _ = context.VerTambien(
            borrados: vista.Es(FiltrosDeVista.Papelera),
            archivados: vista.Es(FiltrosDeVista.Archivados));

        var query = context.Tickets.AsNoTracking().Where(t => t.TenantId == tenantId);

        // Los filtros del panel de navegación.
        //
        // Esto no existía, y el menú ya ofrecía «Mis Tickets» apuntando a `?filter=mine`: el
        // parámetro llegaba, nadie lo leía y la pantalla devolvía los 175 tickets de siempre.
        // Quien lo usaba creía estar viendo los suyos.
        var yo = vista.UsuarioId;

        if (vista.Es(FiltrosDeVista.Mios) && yo.HasValue)
        {
            // «Míos» en un ticket es el agente que lo lleva, no quien lo abrió: el que abre
            // suele ser un cliente y no tiene esta pantalla.
            query = query.Where(t => t.AssignedAgentId == yo.Value);
        }
        else if (vista.Es(FiltrosDeVista.CreadosPorMi) && yo.HasValue)
        {
            query = query.Where(t => t.CustomerId == yo.Value);
        }
        else if (vista.Es(FiltrosDeVista.Favoritos))
        {
            // Sin marcados, la lista es vacía y no «todos». Devolver todo cuando no hay
            // favoritos sería el mismo engaño que se está arreglando, sólo que al revés.
            var marcados = vista.Favoritos.ToArray();
            query = query.Where(t => EF.Constant(marcados).Contains(t.Id));
        }
        else if (vista.Es(FiltrosDeVista.CompartidosConmigo))
        {
            var conmigo = vista.CompartidosConmigo.ToArray();
            query = query.Where(t => EF.Constant(conmigo).Contains(t.Id));
        }
        else if (vista.Es(FiltrosDeVista.Privados) && yo.HasValue)
        {
            // Privado es «lo llevo yo y no se lo he dado a nadie». Se resta lo compartido en
            // lugar de guardar un campo `EsPrivado`, que sería una segunda fuente de verdad y se
            // desincronizaría en cuanto alguien compartiera por otra vía.
            var compartidos = vista.CompartidosConAlguien.ToArray();
            query = query.Where(t => t.AssignedAgentId == yo.Value && !EF.Constant(compartidos).Contains(t.Id));
        }
        else if (vista.Es(FiltrosDeVista.Archivados))
        {
            // El alcance abierto arriba deja pasar lo archivado; aquí se pide **sólo** eso.
            query = query.Where(t => t.ArchivadoEnUtc != null);
        }
        else if (vista.Es(FiltrosDeVista.Papelera))
        {
            query = query.Where(t => t.IsDeleted);
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

        // Búsqueda por texto, sobre **todos** los tickets del inquilino.
        //
        // Va en el servidor y no filtrando en el cliente lo que quepa en una página: con miles de
        // filas, lo que se busca puede no estar entre las primeras y el buscador saldría vacío
        // para algo que sí existe — un fallo que sólo aparece cuando el cliente crece, y que para
        // entonces nadie relaciona con esto.
        //
        // Se busca en el asunto y en la descripción: quien busca «impresora» a veces recuerda una
        // palabra del cuerpo y no del título, y limitarlo al título hace parecer que el dato no
        // está.
        if (pagination.TextoBuscado is { } texto)
            query = query.Where(t => t.Title.Contains(texto) || t.Description.Contains(texto));

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
                t.CreatedAt, t.ResolvedAt, t.Origen, t.SolicitanteNombre, t.SolicitanteEmail, t.SolicitanteTelefono, t.SolicitanteEmpresa, t.Clasificacion, t.TeamId, t.Etiquetas))
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
                t.CreatedAt, t.ResolvedAt, t.Origen, t.SolicitanteNombre, t.SolicitanteEmail, t.SolicitanteTelefono, t.SolicitanteEmpresa, t.Clasificacion, t.TeamId, t.Etiquetas))
            .FirstOrDefaultAsync(ct);
    }
}
