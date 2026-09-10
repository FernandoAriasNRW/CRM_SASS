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
    /// La entrada del panel de navegación ya resuelta. Ver <c>AlcanceDeVista</c>.
    /// </summary>
    BuildingBlocks.Application.AlcanceDeVista? Alcance,
    PaginationRequest Pagination
) : IQuery<PagedResult<ProjectDto>>;
