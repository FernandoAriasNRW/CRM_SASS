using BuildingBlocks.Application.Abstractions;
using Identity.Application.DTOs;
using System;
using System.Collections.Generic;

namespace Identity.Application.Queries;

/// <param name="Search">Texto a buscar en el nombre y el correo, o nulo para traerlos todos.</param>
/// <param name="Limit">Cuántas devolver como mucho, o nulo para no recortar.</param>
public sealed record GetTenantUsersQuery(Guid TenantId, string? Search = null, int? Limit = null)
    : IQuery<IReadOnlyList<UserDto>>;
