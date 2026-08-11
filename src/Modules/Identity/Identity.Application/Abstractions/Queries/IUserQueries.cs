using BuildingBlocks.Domain;
using Identity.Application.DTOs;

namespace Identity.Application.Abstractions.Queries;

public interface IUserQueries
{
    Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserDto?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<PagedResult<UserDto>> GetAllAsync(int page, int pageSize, CancellationToken ct = default);
    Task<System.Collections.Generic.IReadOnlyList<UserDto>> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
}
