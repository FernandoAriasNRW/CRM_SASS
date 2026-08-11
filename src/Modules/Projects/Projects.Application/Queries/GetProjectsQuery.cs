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
    string? Filter,
    Guid? UserId,
    PaginationRequest Pagination
) : IQuery<PagedResult<ProjectDto>>;
