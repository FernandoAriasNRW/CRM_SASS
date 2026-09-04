using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;
using Projects.Application.Abstractions.Queries;
using Projects.Application.DTOs;
using Projects.Infrastructure.Persistence;

namespace Projects.Infrastructure.Queries;

public sealed class ProjectQueries(ProjectsDbContext context) : IProjectQueries
{
    public async Task<PagedResult<ProjectDto>> GetByTenantAsync(
        Guid tenantId, string? status, Guid? ownerId, Guid? spaceId, Guid? folderId, AlcanceDeVista? alcance,
        int page, int pageSize, CancellationToken ct = default)
    {
        var vista = alcance ?? AlcanceDeVista.Ninguno;

        // La papelera y el archivo quedan fuera de lo que el filtro global deja ver, así que hay
        // que abrir el alcance antes de construir la consulta. El ámbito se cierra al terminar.
        using var _ = context.VerTambien(
            borrados: vista.Es(FiltrosDeVista.Papelera),
            archivados: vista.Es(FiltrosDeVista.Archivados));

        var query = context.Projects.AsNoTracking().Where(p => p.TenantId == tenantId);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(p => p.Status.Value == status || p.Status.Name == status);

        if (ownerId.HasValue)
            query = query.Where(p => p.OwnerId == ownerId.Value);
            
        if (spaceId.HasValue)
            query = query.Where(p => p.SpaceId == spaceId.Value);
            
        if (folderId.HasValue) query = query.Where(p => p.FolderId == folderId.Value);

        var yo = vista.UsuarioId;

        if (vista.Es(FiltrosDeVista.Mios) && yo.HasValue)
        {
            query = query.Where(p => p.OwnerId == yo.Value);
        }
        else if (vista.Es(FiltrosDeVista.DeMiEquipo) && yo.HasValue)
        {
            query = query.Where(p => EF.Functions.JsonContains(p.TagIds, yo.Value.ToString()));
        }
        else if (vista.Es(FiltrosDeVista.CreadosPorMi) && yo.HasValue)
        {
            // Un proyecto no guarda quién lo creó, sólo quién lo posee. Se usa el dueño, que
            // es lo más cercano y lo que la gente espera. Si algún día hace falta distinguir
            // «lo abrí yo» de «lo llevo yo», hará falta una columna nueva: fingir la
            // diferencia con el dueño daría dos entradas de menú con la misma lista.
            query = query.Where(p => p.OwnerId == yo.Value);
        }
        else if (vista.Es(FiltrosDeVista.Favoritos))
        {
            var marcados = vista.Favoritos.ToArray();
            query = query.Where(p => EF.Constant(marcados).Contains(p.Id));
        }
        else if (vista.Es(FiltrosDeVista.CompartidosConmigo))
        {
            var conmigo = vista.CompartidosConmigo.ToArray();
            query = query.Where(p => EF.Constant(conmigo).Contains(p.Id));
        }
        else if (vista.Es(FiltrosDeVista.Privados) && yo.HasValue)
        {
            var compartidos = vista.CompartidosConAlguien.ToArray();
            query = query.Where(p => p.OwnerId == yo.Value && !EF.Constant(compartidos).Contains(p.Id));
        }
        else if (vista.Es(FiltrosDeVista.Archivados))
        {
            query = query.Where(p => p.ArchivadoEnUtc != null);
        }
        else if (vista.Es(FiltrosDeVista.Papelera))
        {
            query = query.Where(p => p.IsDeleted);
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.StartDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new ProjectDto(p.Id, p.TenantId, p.SpaceId, p.FolderId, p.Name.Value, p.Description,
                p.StartDate, p.EstimatedEndDate, p.Status.Value, p.OwnerId))
            .ToListAsync(ct);

        return PagedResult<ProjectDto>.Create(items, totalCount, page, pageSize);
    }

    public async Task<ProjectDto?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await context.Projects.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.Id == id)
            .Select(p => new ProjectDto(p.Id, p.TenantId, p.SpaceId, p.FolderId, p.Name.Value, p.Description,
                p.StartDate, p.EstimatedEndDate, p.Status.Value, p.OwnerId))
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