using Identity.Application.Favoritos;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

public sealed class RepositorioDeFavoritos(IdentityDbContext contexto) : IRepositorioDeFavoritos
{
    /// <summary>
    /// Sólo los identificadores, no las filas enteras.
    ///
    /// Es lo único que necesita el filtro —«dame las tareas cuyo id esté en esta lista»— y traer
    /// el resto de la fila sería mover datos que nadie va a mirar. Van ordenados por cuándo se
    /// marcaron, de lo más reciente a lo más antiguo, que es el orden en que la gente espera ver
    /// sus favoritos.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> IdsDeAsync(Guid tenantId, Guid userId, string tipo, CancellationToken ct)
        => await contexto.Favoritos
            .AsNoTracking()
            .Where(f => f.TenantId == tenantId && f.UserId == userId && f.Tipo == tipo)
            .OrderByDescending(f => f.MarcadoUtc)
            .Select(f => f.EntityId)
            .ToListAsync(ct);

    public Task<Favorito?> BuscarAsync(Guid tenantId, Guid userId, string tipo, Guid entityId, CancellationToken ct)
        => contexto.Favoritos.FirstOrDefaultAsync(
            f => f.TenantId == tenantId && f.UserId == userId && f.Tipo == tipo && f.EntityId == entityId, ct);

    public Task<int> CuantosAsync(Guid tenantId, Guid userId, string tipo, CancellationToken ct)
        => contexto.Favoritos.CountAsync(
            f => f.TenantId == tenantId && f.UserId == userId && f.Tipo == tipo, ct);

    public async Task AnadirAsync(Favorito favorito, CancellationToken ct)
        => await contexto.Favoritos.AddAsync(favorito, ct);

    public void Quitar(Favorito favorito) => contexto.Favoritos.Remove(favorito);

    public Task GuardarAsync(CancellationToken ct) => contexto.SaveChangesAsync(ct);
}

/// <summary>
/// Responde el puerto <see cref="BuildingBlocks.Application.Abstractions.IUserFavorites"/>
/// para el usuario de la petición en curso.
///
/// Lo implementa Identity porque es quien guarda los favoritos, y lo consumen los demás módulos
/// sin conocerlo: la dependencia va de todos a BuildingBlocks, nunca entre módulos.
/// </summary>
public sealed class FavoritosDelUsuario(
    IRepositorioDeFavoritos repositorio,
    BuildingBlocks.Application.Abstractions.IUserContext usuario)
    : BuildingBlocks.Application.Abstractions.IUserFavorites
{
    public Task<IReadOnlyList<Guid>> GetIdsAsync(string tipo, CancellationToken ct = default)
        => repositorio.IdsDeAsync(usuario.TenantId, usuario.UserId, tipo, ct);
}
