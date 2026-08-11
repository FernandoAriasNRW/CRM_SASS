using BuildingBlocks.Application.Abstractions;
using Identity.Application.DTOs;
using System;
using System.Collections.Generic;

namespace Identity.Application.Queries;

public sealed record GetTenantUsersQuery(Guid TenantId) : IQuery<IReadOnlyList<UserDto>>;
