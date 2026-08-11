using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Projects.Application.Abstractions.Queries;
using Projects.Application.DTOs;
using Projects.Application.Queries;

namespace Projects.Application.Handlers.Queries;

public sealed class GetProjectsQueryHandler(IProjectQueries queries)
    : IQueryHandler<GetProjectsQuery, PagedResult<ProjectDto>>
{
  public async Task<Result<PagedResult<ProjectDto>>> Handle(GetProjectsQuery request, CancellationToken cancellationToken)
  {
    var result = await queries.GetByTenantAsync(
        request.TenantId, request.Status, request.OwnerId, request.SpaceId, request.FolderId, request.Filter, request.UserId,
        request.Pagination.Page, request.Pagination.PageSize, cancellationToken);

    return Result<PagedResult<ProjectDto>>.Success(result);
  }
}

public sealed class GetProjectByIdQueryHandler(IProjectQueries queries)
    : IQueryHandler<GetProjectByIdQuery, ProjectDto?>
{
  public async Task<Result<ProjectDto?>> Handle(GetProjectByIdQuery request, CancellationToken cancellationToken)
  {
    var project = await queries.GetByIdAsync(request.TenantId, request.Id, cancellationToken);
    return Result<ProjectDto?>.Success(project);
  }
}
