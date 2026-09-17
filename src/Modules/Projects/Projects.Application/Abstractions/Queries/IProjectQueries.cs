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
        BuildingBlocks.Application.ViewScope? viewScope,
        // El objeto de paginación entero y no `page, pageSize` sueltos: es el que lleva el texto
        // buscado, y así los tres módulos con listado reciben lo mismo. Con dos enteros habría que
        // añadir un parámetro más por cada opción de listado que aparezca.
        PaginationRequest pagination,
        CancellationToken ct = default);

    Task<ProjectDto?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<IEnumerable<SpaceDto>> GetSpacesAsync(Guid tenantId, CancellationToken ct = default);
    
    Task<IEnumerable<FolderDto>> GetFoldersAsync(Guid tenantId, Guid spaceId, CancellationToken ct = default);
}
