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

  public async Task<System.Collections.Generic.IReadOnlyList<UserDto>> GetByTenantIdAsync(
      Guid tenantId, string? buscar = null, int? tope = null, CancellationToken ct = default)
  {
    var consulta = context.User.AsNoTracking().Where(u => u.TenantId == tenantId);

    // Se busca en el nombre y en el correo. El correo importa: en una empresa con dos «Ana
    // García» es lo único que las distingue, y quien menciona a alguien suele acordarse de una de
    // las dos cosas.
    //
    // Sin normalizar acentos ni mayúsculas: la colación de la base (`utf8mb4_0900_ai_ci`) es
    // insensible a las dos cosas, así que «garcia» encuentra «García».
    if (!string.IsNullOrWhiteSpace(buscar))
    {
      var texto = buscar.Trim();
      consulta = consulta.Where(u => u.Name.Contains(texto) || u.Email.Value.Contains(texto));
    }

    var ordenada = consulta.OrderBy(u => u.Name);

    // El tope es opcional y sólo se aplica si lo piden: la pantalla de administración quiere la
    // lista entera, y el desplegable de menciones cinco. Sin él, ese desplegable se descargaba
    // todas las personas del inquilino —cientos, en esta base— para enseñar cinco.
    //
    // No se pone un máximo por defecto a propósito: cambiaría en silencio lo que ya reciben los
    // que llaman hoy, y una lista recortada sin avisar es peor que una larga.
    return await (tope is { } cuantos ? ordenada.Take(cuantos) : ordenada)
        .Select(u => UserDto.FromEntity(u))
        .ToListAsync(ct);
  }
}