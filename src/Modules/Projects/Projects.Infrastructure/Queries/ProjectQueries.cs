using BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;
using Projects.Application.Abstractions.Queries;
using Projects.Application.DTOs;
using Projects.Infrastructure.Persistence;

namespace Projects.Infrastructure.Queries;

public sealed class ProjectQueries(ProjectsDbContext context) : IProjectQueries
{
    public async Task<PagedResult<ProjectDto>> GetByTenantAsync(
        Guid tenantId, string? status, Guid? ownerId, Guid? spaceId, Guid? folderId, string? filter, Guid? userId,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.Projects.AsNoTracking().Where(p => p.TenantId == tenantId);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(p => p.Status.Value == status || p.Status.Name == status);

        if (ownerId.HasValue)
            query = query.Where(p => p.OwnerId == ownerId.Value);
            
        if (spaceId.HasValue)
            query = query.Where(p => p.SpaceId == spaceId.Value);
            
        if (folderId.HasValue) query = query.Where(p => p.FolderId == folderId.Value);

        if (!string.IsNullOrWhiteSpace(filter) && userId.HasValue)
        {
            if (filter.Equals("mine", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.OwnerId == userId.Value);
            }
            else if (filter.Equals("team", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => EF.Functions.JsonContains(p.TagIds, userId.Value.ToString()));
            }
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