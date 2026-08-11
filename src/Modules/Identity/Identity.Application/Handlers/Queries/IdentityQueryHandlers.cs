using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Application.Abstractions.Queries;
using Identity.Application.DTOs;
using Identity.Application.Queries;

namespace Identity.Application.Handlers.Queries;

public sealed class GetCurrentUserQueryHandler(IUserQueries userQueries)
    : IQueryHandler<GetCurrentUserQuery, UserDto?>
{
    public async Task<Result<UserDto?>> Handle(GetCurrentUserQuery request, CancellationToken ct)
    {
        var user = await userQueries.GetByIdAsync(request.UserId, ct);
        return Result<UserDto?>.Success(user);
    }
}

public sealed class GetUsersQueryHandler(IUserQueries userQueries)
    : IQueryHandler<GetUsersQuery, PagedResult<UserDto>>
{
    public async Task<Result<PagedResult<UserDto>>> Handle(GetUsersQuery request, CancellationToken ct)
    {
        var result = await userQueries.GetAllAsync(request.Page, request.PageSize, ct);
        return Result<PagedResult<UserDto>>.Success(result);
    }
}

public sealed class GetUserByIdQueryHandler(IUserQueries userQueries)
    : IQueryHandler<GetUserByIdQuery, UserDto?>
{
    public async Task<Result<UserDto?>> Handle(GetUserByIdQuery request, CancellationToken ct)
    {
        var user = await userQueries.GetByIdAsync(request.Id, ct);
        return Result<UserDto?>.Success(user);
    }
}
