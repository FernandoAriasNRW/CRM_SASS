using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Application.DTOs;

namespace Identity.Application.Queries;

public sealed record GetUserByIdQuery(Guid Id) : IQuery<UserDto?>;
