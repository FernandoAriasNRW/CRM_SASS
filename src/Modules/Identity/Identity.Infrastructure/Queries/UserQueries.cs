using BuildingBlocks.Domain;
using Identity.Application.Abstractions.Queries;
using Identity.Application.DTOs;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace Identity.Infrastructure.Queries;

public sealed class UserQueries(IdentityDbContext context) : IUserQueries
{
  public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
  {
    return await context.User
        .AsNoTracking()
        .Where(u => u.Id == id)
        .Select(u => UserDto.FromEntity(u))
        .FirstOrDefaultAsync(ct);
  }

  public async Task<UserDto?> GetByEmailAsync(string email, CancellationToken ct = default)
  {
    var normalized = email.ToLowerInvariant();
    return await context.User
        .AsNoTracking()
        .Where(u => u.Email.Value == normalized)
        .Select(u => UserDto.FromEntity(u))
        .FirstOrDefaultAsync(ct);
  }

  public async Task<PagedResult<UserDto>> GetAllAsync(int page, int pageSize, CancellationToken ct = default)
  {
    var query = context.User.AsNoTracking();
    var totalCount = await query.CountAsync(ct);

    var items = await query
        .OrderBy(u => u.Name)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(u => UserDto.FromEntity(u))
        .ToListAsync(ct);

    return PagedResult<UserDto>.Create(items, totalCount, page, pageSize);
  }

  public async Task<System.Collections.Generic.IReadOnlyList<UserDto>> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
  {
    return await context.User
        .AsNoTracking()
        .Where(u => u.TenantId == tenantId)
        .OrderBy(u => u.Name)
        .Select(u => UserDto.FromEntity(u))
        .ToListAsync(ct);
  }
}