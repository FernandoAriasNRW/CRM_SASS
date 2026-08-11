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
    PaginationRequest Pagination
) : IQuery<PagedResult<TicketDto>>;
