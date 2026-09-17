using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Projects.Application.DTOs;

namespace Projects.Application.Queries;

public sealed record GetProjectsQuery(
    Guid TenantId,
    string? Status,
    Guid? OwnerId,
    Guid? SpaceId,
    Guid? FolderId,
    /// <summary>
    /// La entrada del panel de navegación ya resuelta. Ver <c>ViewScope</c>.
    /// </summary>
    BuildingBlocks.Application.ViewScope? ViewScope,
    PaginationRequest Pagination
) : IQuery<PagedResult<ProjectDto>>;
