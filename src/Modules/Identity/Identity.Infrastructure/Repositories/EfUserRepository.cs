using Identity.Application.Abstractions.Repositories;
using Identity.Domain.Entities;
using Identity.Domain.ValueObjects;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

public sealed class EfUserRepository(IdentityDbContext context) : IUserRepository
{
  /// <summary>
  /// Consultas que deben ejecutarse sin el filtro de tenant.
  ///
  /// Identificar a un usuario por su correo es, por naturaleza, previo al tenant: es esa
  /// misma consulta la que descubre a cuál pertenece. Con el filtro activo, al iniciar
  /// sesión no hay tenant todavía, el filtro no casa con ninguna fila y el usuario nunca
  /// se encuentra.
  ///
  /// <c>IgnoreQueryFilters</c> desactiva TODOS los filtros globales, incluido el de soft
  /// delete, así que hay que reponer <c>!IsDeleted</c> a mano: de lo contrario un usuario
  /// dado de baja volvería a poder iniciar sesión.
  /// </summary>
  private IQueryable<User> UsuariosDeTodosLosTenants =>
      context.User.IgnoreQueryFilters().Where(u => !u.IsDeleted);

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
    return await UsuariosDeTodosLosTenants.FirstOrDefaultAsync(u => u.Email.Value == normalized, ct);
  }

  public async Task<User?> FindForSessionRenewalAsync(Guid userId, CancellationToken ct = default)
      => await UsuariosDeTodosLosTenants.FirstOrDefaultAsync(u => u.Id == userId, ct);

  public async Task<bool> EmailExistsAsync(string email, Guid? excludeUserId = null, CancellationToken ct = default)
  {
    // La unicidad del correo es global, no por tenant: si se comprobara sólo dentro del
    // tenant actual, dos organizaciones podrían registrar el mismo correo y el inicio de
    // sesión, que busca sin filtrar, dejaría de poder distinguirlos.
    var normalized = email.ToLowerInvariant();
    var query = UsuariosDeTodosLosTenants.Where(u => u.Email.Value == normalized);
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
