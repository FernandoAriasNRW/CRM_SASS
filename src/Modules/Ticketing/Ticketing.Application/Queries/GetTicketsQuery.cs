using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Ticketing.Application.DTOs;

namespace Ticketing.Application.Queries;

public sealed record GetTicketsQuery(
    Guid TenantId,
    Guid? CustomerId,
    Guid? AgentId,
    string? Priority,
    string? Status,
    PaginationRequest Pagination,
    /// <summary>Uno de <c>FiltrosDeVista</c>, o nulo. Lo manda el panel de navegación.</summary>
    string? Filter = null,
    /// <summary>Quien pregunta. Sin esto, «mis tickets» no significa nada.</summary>
    Guid? UserId = null,
    /// <summary>Los que esa persona marcó, cuando el filtro es «favoritos».</summary>
    IReadOnlyList<Guid>? IdsFavoritos = null
) : IQuery<PagedResult<TicketDto>>;
