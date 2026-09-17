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
    /// <summary>
    /// La entrada del panel de navegación que se ha pulsado, ya resuelta: qué filtro es, quién
    /// pregunta y las listas que sólo se pueden saber fuera del módulo.
    ///
    /// Iba como tres parámetros sueltos —filtro, usuario, favoritos— y cada concepto nuevo del
    /// menú añadía otro. Ver <c>ViewScope</c>.
    /// </summary>
    BuildingBlocks.Application.ViewScope? Alcance = null
) : IQuery<PagedResult<TicketDto>>;
