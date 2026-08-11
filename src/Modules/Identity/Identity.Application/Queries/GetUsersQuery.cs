using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Application.DTOs;

namespace Identity.Application.Queries;

public sealed record GetUsersQuery(int Page = 1, int PageSize = 20) : IQuery<PagedResult<UserDto>>;
