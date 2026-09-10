using Microsoft.EntityFrameworkCore;
using Notifications.Application.Preferencias;
using Notifications.Domain.Entities;

namespace Notifications.Infrastructure.Persistence;

public sealed class RepositorioDePreferencias(NotificationsDbContext contexto) : IRepositorioDePreferencias
{
    /// <summary>
    /// Las de una persona, o <c>null</c> si nunca las ha guardado.
    ///
    /// El filtro por inquilino ya lo aplica el contexto, pero se repite aquí por escrito: es la
    /// diferencia entre depender de un filtro global que alguien puede desactivar sin querer y
    /// decir en la consulta qué se busca. Cuesta una condición.
    /// </summary>
    public Task<PreferenciasDeNotificacion?> DeLaPersonaAsync(Guid tenantId, Guid userId, CancellationToken ct) =>
        contexto.PreferenciasDeNotificacion
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.UserId == userId, ct);

    public async Task AñadirAsync(PreferenciasDeNotificacion preferencias, CancellationToken ct) =>
        await contexto.PreferenciasDeNotificacion.AddAsync(preferencias, ct);

    public Task GuardarAsync(CancellationToken ct) => contexto.SaveChangesAsync(ct);
}
