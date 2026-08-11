using BuildingBlocks.Domain;
using Ticketing.Application.DTOs;

namespace Ticketing.Application.Abstractions.Queries;

public interface ITicketQueries
{
    Task<PagedResult<TicketDto>> GetByTenantAsync(
        Guid tenantId,
        Guid? customerId,
        Guid? agentId,
        string? priority,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<PagedResult<TicketDto>> GetByTenantWithPaginationAsync(Guid tenantId, Guid? customerId, Guid? agentId, string? priority, string? status, PaginationRequest pagination, CancellationToken ct = default);

    Task<TicketDto?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
}
