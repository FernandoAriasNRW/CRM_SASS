using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Application.Abstractions.Queries;
using Identity.Application.DTOs;
using Identity.Application.Queries;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.Handlers.Queries;

public sealed class GetTenantUsersQueryHandler(IUserQueries userQueries)
    : IQueryHandler<GetTenantUsersQuery, IReadOnlyList<UserDto>>
{
    public async Task<Result<IReadOnlyList<UserDto>>> Handle(GetTenantUsersQuery request, CancellationToken ct)
    {
        var result = await userQueries.GetByTenantIdAsync(request.TenantId, ct);
        return Result<IReadOnlyList<UserDto>>.Success(result);
    }
}
