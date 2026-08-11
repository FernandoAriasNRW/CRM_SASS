using Identity.Domain.Entities;

namespace Identity.Application.Abstractions.Repositories;

public interface IUserRepository
{
  Task<User?> GetByIdAsync(Guid id, bool includeDeleted, CancellationToken ct = default);

  Task<User?> FindByEmailAsync(string email, CancellationToken ct = default);

  Task<bool> EmailExistsAsync(string email, Guid? excludeUserId = null, CancellationToken ct = default);

  Task AddAsync(User user, CancellationToken ct = default);

  Task UpdateAsync(User user, CancellationToken ct = default);

  Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

  Task<int> GetAdminCountAsync(CancellationToken ct);
}