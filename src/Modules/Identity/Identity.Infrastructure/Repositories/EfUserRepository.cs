using Identity.Application.Abstractions.Repositories;
using Identity.Domain.Entities;
using Identity.Domain.ValueObjects;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

public sealed class EfUserRepository(IdentityDbContext context) : IUserRepository
{
  public async Task<User?> GetByIdAsync(Guid id, bool includeDeleted, CancellationToken ct = default)
  {
    var query = context.User.AsQueryable();

    if (!includeDeleted)
      query = query.Where(u => !u.IsDeleted);

    return await query.FirstOrDefaultAsync(u => u.Id == id, ct);
  }

  public async Task<User?> FindByEmailAsync(string email, CancellationToken ct = default)
  {
    var normalized = email.ToLowerInvariant();
    return await context.User.FirstOrDefaultAsync(u => u.Email.Value == normalized, ct);
  }

  public async Task<bool> EmailExistsAsync(string email, Guid? excludeUserId = null, CancellationToken ct = default)
  {
    var normalized = email.ToLowerInvariant();
    var query = context.User.Where(u => u.Email.Value == normalized);
    if (excludeUserId.HasValue)
      query = query.Where(u => u.Id != excludeUserId.Value);
    return await query.AnyAsync(ct);
  }

  public async Task AddAsync(User user, CancellationToken ct = default)
      => await context.User.AddAsync(user, ct);

  public Task UpdateAsync(User user, CancellationToken ct = default)
  {
    context.User.Update(user);
    return Task.CompletedTask;
  }

  public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
      => await context.User.AnyAsync(u => u.Id == id, ct);

  public Task<int> GetAdminCountAsync(CancellationToken ct)
      => context.User.CountAsync(u => !u.IsDeleted && u.Role.Value == UserRole.Admin.Value, ct);
}
