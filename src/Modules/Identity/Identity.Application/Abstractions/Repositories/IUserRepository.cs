using Identity.Domain.Entities;

namespace Identity.Application.Abstractions.Repositories;

public interface IUserRepository
{
  Task<User?> GetByIdAsync(Guid id, bool includeDeleted, CancellationToken ct = default);

  Task<User?> FindByEmailAsync(string email, CancellationToken ct = default);

  /// <summary>
  /// Busca al usuario para renovar su sesión, sin filtrar por tenant.
  ///
  /// Existe como método aparte y no como una variante de <see cref="GetByIdAsync"/>
  /// porque saltarse el aislamiento debe verse en el punto de llamada. La renovación
  /// ocurre sin sesión activa —sólo con la cookie de refresco—, así que todavía no hay
  /// tenant con el que filtrar; el resto de flujos sí lo tienen y deben conservarlo.
  /// </summary>
  Task<User?> FindForSessionRenewalAsync(Guid userId, CancellationToken ct = default);

  Task<bool> EmailExistsAsync(string email, Guid? excludeUserId = null, CancellationToken ct = default);

  Task AddAsync(User user, CancellationToken ct = default);

  Task UpdateAsync(User user, CancellationToken ct = default);

  Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

  Task<int> GetAdminCountAsync(CancellationToken ct);
}