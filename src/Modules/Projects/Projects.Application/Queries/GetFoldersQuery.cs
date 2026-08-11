using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Projects.Application.Abstractions.Queries;
using Projects.Application.DTOs;

namespace Projects.Application.Queries;

public sealed record GetFoldersQuery(Guid TenantId, Guid SpaceId) : IQuery<IEnumerable<FolderDto>>;

public sealed class GetFoldersQueryHandler(IProjectQueries queries) : IQueryHandler<GetFoldersQuery, IEnumerable<FolderDto>>
{
    public async Task<Result<IEnumerable<FolderDto>>> Handle(GetFoldersQuery request, CancellationToken cancellationToken)
    {
        var folders = await queries.GetFoldersAsync(request.TenantId, request.SpaceId, cancellationToken);
        return Result<IEnumerable<FolderDto>>.Success(folders);
    }
}
