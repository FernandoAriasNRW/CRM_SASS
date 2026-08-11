using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Ticketing.Application.Abstractions.Queries;
using Ticketing.Application.Abstractions.Repositories;
using Ticketing.Application.DTOs;
using Ticketing.Application.Queries;

namespace Ticketing.Application.Handlers.Queries;

public sealed class GetTicketByIdHandler(ITicketRepository repository) : IQueryHandler<GetTicketByIdQuery, TicketDto?>
{
    public async Task<Result<TicketDto?>> Handle(GetTicketByIdQuery request, CancellationToken cancellationToken)
    {
        var ticket = await repository.GetByIdAsync(request.TenantId, request.TicketId, cancellationToken);
        return Result<TicketDto?>.Success(ticket is null ? null : TicketDto.FromEntity(ticket));
    }
}

public sealed class GetTicketsHandler(ITicketQueries queries) : IQueryHandler<GetTicketsQuery, PagedResult<TicketDto>>
{
    public async Task<Result<PagedResult<TicketDto>>> Handle(GetTicketsQuery request, CancellationToken cancellationToken)
    {
        var result = await queries.GetByTenantWithPaginationAsync(
            request.TenantId, request.CustomerId, request.AgentId,
            request.Priority, request.Status,
            request.Pagination, cancellationToken);

        return Result<PagedResult<TicketDto>>.Success(result);
    }
}
