using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;
using Projects.Application.Abstractions.Queries;
using Projects.Application.DTOs;
using Projects.Domain.Entities;
using Projects.Infrastructure.Persistence;

namespace Projects.Infrastructure.Queries;

public sealed class ProjectQueries(ProjectsDbContext context) : IProjectQueries
{
    public async Task<PagedResult<ProjectDto>> GetByTenantAsync(
        Guid tenantId, string? status, Guid? ownerId, Guid? spaceId, Guid? folderId, ViewScope? viewScope,
        PaginationRequest pagination, CancellationToken ct = default)
    {
        var scope = viewScope ?? ViewScope.None;

        // La papelera y el archivo quedan fuera de lo que el filtro global deja ver, así que hay
        // que abrir el alcance antes de construir la consulta. El ámbito se cierra al terminar.
        using var _ = context.IncludeHidden(
            deleted: scope.Is(ViewFilters.Trash),
            archived: scope.Is(ViewFilters.Archived));

        var query = context.Projects.AsNoTracking().Where(p => p.TenantId == tenantId);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(p => p.Status.Value == status || p.Status.Name == status);

        if (ownerId.HasValue)
            query = query.Where(p => p.OwnerId == ownerId.Value);
            
        if (spaceId.HasValue)
            query = query.Where(p => p.SpaceId == spaceId.Value);
            
        if (folderId.HasValue) query = query.Where(p => p.FolderId == folderId.Value);

        var me = scope.UserId;

        if (scope.Is(ViewFilters.Mine) && me.HasValue)
        {
            query = query.Where(p => p.OwnerId == me.Value);
        }
        else if (scope.Is(ViewFilters.MyTeam) && me.HasValue)
        {
            // Los proyectos que lleva gente de mis equipos.
            var teamMates = scope.TeamMateIds.ToArray();
            query = query.Where(p => EF.Constant(teamMates).Contains(p.OwnerId));
        }
        else if (scope.Is(ViewFilters.CreatedByMe) && me.HasValue)
        {
            // Un proyecto no guarda quién lo creó, sólo quién lo posee. Se usa el dueño, que
            // es lo más cercano y lo que la gente espera. Si algún día hace falta distinguir
            // «lo abrí yo» de «lo llevo yo», hará falta una columna nueva: fingir la
            // diferencia con el dueño daría dos entradas de menú con la misma lista.
            query = query.Where(p => p.OwnerId == me.Value);
        }
        else if (scope.Is(ViewFilters.Favorites))
        {
            var favorites = scope.Favorites.ToArray();
            query = query.Where(p => EF.Constant(favorites).Contains(p.Id));
        }
        else if (scope.Is(ViewFilters.SharedWithMe))
        {
            var sharedWithMe = scope.SharedWithMe.ToArray();
            query = query.Where(p => EF.Constant(sharedWithMe).Contains(p.Id));
        }
        else if (scope.Is(ViewFilters.Private) && me.HasValue)
        {
            var sharedWithOthers = scope.SharedWithOthers.ToArray();
            query = query.Where(p => p.OwnerId == me.Value && !EF.Constant(sharedWithOthers).Contains(p.Id));
        }
        else if (scope.Is(ViewFilters.Archived))
        {
            query = query.Where(p => p.ArchivedAtUtc != null);
        }
        else if (scope.Is(ViewFilters.Trash))
        {
            query = query.Where(p => p.IsDeleted);
        }

        // Búsqueda por texto, sobre **todos** los proyectos del inquilino. Ver la nota equivalente
        // en TicketQueries.
        if (pagination.SearchText is { } text)
            query = query.Where(p => p.Name.Value.Contains(text) || p.Description.Contains(text));

        // «Desde» y «Hasta» de la pantalla, sobre la fecha de inicio. Se mandaban y se ignoraban.
        if (pagination.StartDate is { } from)
        {
            var fromDate = DateOnly.FromDateTime(from);
            query = query.Where(p => p.StartDate >= fromDate);
        }
        if (pagination.EndDate is { } to)
        {
            var toDate = DateOnly.FromDateTime(to);
            query = query.Where(p => p.StartDate <= toDate);
        }

        var totalCount = await query.CountAsync(ct);

        // El orden de la tabla. Se mandaba y se ignoraba: pulsar una cabecera no cambiaba nada.
        var desc = pagination.SortDirection?.ToLower() == "desc";
        IOrderedQueryable<Project> ordered = pagination.SortColumn?.ToLower() switch
        {
            "name" => desc ? query.OrderByDescending(p => p.Name.Value) : query.OrderBy(p => p.Name.Value),
            "status" => desc ? query.OrderByDescending(p => p.Status.Value) : query.OrderBy(p => p.Status.Value),
            "ownerid" => desc ? query.OrderByDescending(p => p.OwnerId) : query.OrderBy(p => p.OwnerId),
            "startdate" => desc ? query.OrderByDescending(p => p.StartDate) : query.OrderBy(p => p.StartDate),
            "estimatedenddate" => desc ? query.OrderByDescending(p => p.EstimatedEndDate) : query.OrderBy(p => p.EstimatedEndDate),
            "createdatutc" => desc ? query.OrderByDescending(p => p.CreatedAtUtc) : query.OrderBy(p => p.CreatedAtUtc),
            _ => query.OrderByDescending(p => p.StartDate)
        };

        var items = await ordered
            // A igualdad, el más reciente primero: quien acaba de crear un proyecto espera verlo
            // arriba, no en un sitio cualquiera entre los que empiezan el mismo día.
            .ThenByDescending(p => p.CreatedAtUtc)
            // Y por Id, para que el orden sea total: ver TaskQueries. Sin él, dos filas iguales
            // podían salir en las dos páginas, o en ninguna.
            .ThenBy(p => p.Id)
            .Skip(pagination.Skip).Take(pagination.Take)
            .Select(p => new ProjectDto(p.Id, p.TenantId, p.SpaceId, p.FolderId, p.Name.Value, p.Description,
                p.StartDate, p.EstimatedEndDate, p.Status.Value, p.OwnerId, p.TagIds, p.CreatedAtUtc))
            .ToListAsync(ct);

        return PagedResult<ProjectDto>.Create(items, totalCount, pagination.Page, pagination.PageSize);
    }

    public async Task<ProjectDto?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await context.Projects.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.Id == id)
            .Select(p => new ProjectDto(p.Id, p.TenantId, p.SpaceId, p.FolderId, p.Name.Value, p.Description,
                p.StartDate, p.EstimatedEndDate, p.Status.Value, p.OwnerId, p.TagIds, p.CreatedAtUtc))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IEnumerable<SpaceDto>> GetSpacesAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await context.Spaces
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .Select(s => new SpaceDto(s.Id, s.Name, s.Description, s.Color))
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<FolderDto>> GetFoldersAsync(Guid tenantId, Guid spaceId, CancellationToken ct = default)
    {
        return await context.Folders
            .AsNoTracking()
            .Where(f => f.TenantId == tenantId && f.SpaceId == spaceId)
            .Select(f => new FolderDto(f.Id, f.SpaceId, f.Name))
            .ToListAsync(ct);
    }
}