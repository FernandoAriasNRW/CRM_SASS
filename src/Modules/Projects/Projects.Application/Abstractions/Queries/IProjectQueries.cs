using BuildingBlocks.Domain;
using Projects.Application.DTOs;

namespace Projects.Application.Abstractions.Queries;

public interface IProjectQueries
{
    Task<PagedResult<ProjectDto>> GetByTenantAsync(
        Guid tenantId,
        string? status,
        Guid? ownerId,
        Guid? spaceId,
        Guid? folderId,
        string? filter,
        Guid? userId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<ProjectDto?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<IEnumerable<SpaceDto>> GetSpacesAsync(Guid tenantId, CancellationToken ct = default);
    
    Task<IEnumerable<FolderDto>> GetFoldersAsync(Guid tenantId, Guid spaceId, CancellationToken ct = default);
}
