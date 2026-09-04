using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Application.Comparticion;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Las comparticiones nominales, sacadas de la tabla de permisos por entidad.
///
/// Todas las consultas exigen <c>UserId != null</c> y <c>EntityId != Guid.Empty</c>: en la misma
/// tabla conviven los permisos por rol y los de módulo entero, y contarlos como comparticiones
/// haría que «compartido conmigo» devolviera media aplicación —otra entrada de menú que promete
/// y no filtra—.
/// </summary>
public sealed class RepositorioDeComparticion(IdentityDbContext contexto) : IRepositorioDeComparticion
{
    private IQueryable<EntityPermission> Nominales(Guid tenantId, string tipoEnPermisos)
        => contexto.EntityPermissions
            .Where(p => p.TenantId == tenantId
                        && p.EntityType == tipoEnPermisos
                        && p.UserId != null
                        && p.EntityId != Guid.Empty
                        && p.PermissionLevel != "None");

    public Task<EntityPermission?> BuscarAsync(Guid tenantId, Guid userId, string tipoEnPermisos, Guid entityId, CancellationToken ct)
        => contexto.EntityPermissions.FirstOrDefaultAsync(
            p => p.TenantId == tenantId && p.UserId == userId
                 && p.EntityType == tipoEnPermisos && p.EntityId == entityId, ct);

    public async Task<IReadOnlyList<Guid>> ConQuienAsync(Guid tenantId, string tipoEnPermisos, Guid entityId, CancellationToken ct)
        => await Nominales(tenantId, tipoEnPermisos).AsNoTracking()
            .Where(p => p.EntityId == entityId)
            .Select(p => p.UserId!.Value)
            .Distinct()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> CompartidosConAsync(Guid tenantId, Guid userId, string tipoEnPermisos, CancellationToken ct)
        => await Nominales(tenantId, tipoEnPermisos).AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.EntityId)
            .Distinct()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> CompartidosConAlguienAsync(Guid tenantId, string tipoEnPermisos, CancellationToken ct)
        => await Nominales(tenantId, tipoEnPermisos).AsNoTracking()
            .Select(p => p.EntityId)
            .Distinct()
            .ToListAsync(ct);

    public async Task AnadirAsync(EntityPermission permiso, CancellationToken ct)
        => await contexto.EntityPermissions.AddAsync(permiso, ct);

    public void Quitar(EntityPermission permiso) => contexto.EntityPermissions.Remove(permiso);
}

/// <summary>
/// Responde el puerto <see cref="IVisibilidadDeEntidades"/> para el usuario de la petición.
///
/// Lo implementa Identity porque es quien guarda los permisos, y lo consumen los demás módulos
/// sin conocerlo, igual que con los favoritos: la dependencia va de todos a BuildingBlocks,
/// nunca entre módulos.
/// </summary>
public sealed class VisibilidadDeEntidades(
    IRepositorioDeComparticion repositorio,
    IUserContext usuario) : IVisibilidadDeEntidades
{
    public Task<IReadOnlyList<Guid>> CompartidosConmigoAsync(string tipoDeEntidad, CancellationToken ct = default)
        => repositorio.CompartidosConAsync(
            usuario.TenantId, usuario.UserId,
            VocabularioDePermisos.ComoLoLlamaElPermiso(tipoDeEntidad), ct);

    public Task<IReadOnlyList<Guid>> CompartidosConAlguienAsync(string tipoDeEntidad, CancellationToken ct = default)
        => repositorio.CompartidosConAlguienAsync(
            usuario.TenantId,
            VocabularioDePermisos.ComoLoLlamaElPermiso(tipoDeEntidad), ct);
}
