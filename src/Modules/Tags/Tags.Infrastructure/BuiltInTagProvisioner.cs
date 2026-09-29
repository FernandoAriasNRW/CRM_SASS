using Microsoft.EntityFrameworkCore;
using Tags.Application.Abstractions;
using Tags.Application.BuiltIn;
using Tags.Domain.Entities;
using Tags.Infrastructure.Persistence;

namespace Tags.Infrastructure;

internal sealed class BuiltInTagProvisioner(TagsDbContext dbContext) : IBuiltInTagProvisioner
{
    /// <summary>
    /// Entrega las predefinidas que la organización no ha recibido nunca, no las que le faltan:
    /// una borrada o renombrada no vuelve (ver <see cref="ProvisionedBuiltInTag"/>).
    /// </summary>
    public async Task<int> ProvisionAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        // Fuera de una petición el filtro compara contra Guid.Empty y las tablas se ven vacías: sin
        // fijar el inquilino, cada arranque volvería a crear todas las predefinidas.
        using var _ = dbContext.AsTenant(tenantId);

        var delivered = (await dbContext.ProvisionedBuiltInTags
                .Where(p => p.TenantId == tenantId)
                .Select(p => p.BuiltInKey)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var pending = BuiltInTags.All.Where(b => !delivered.Contains(b.Key)).ToList();
        if (pending.Count == 0)
            return 0;

        var existing = await dbContext.Tags
            .Where(t => t.TenantId == tenantId)
            .Select(t => new { t.Category, t.Name })
            .ToListAsync(cancellationToken);

        var created = 0;
        foreach (var builtIn in pending)
        {
            // Si la organización ya tiene a mano una etiqueta con ese nombre en esa categoría, se da
            // por entregada sin crearla: crearla chocaría con el índice único.
            var alreadyThere = existing.Any(t => t.Category == builtIn.Category
                && string.Equals(t.Name, builtIn.SpanishName, StringComparison.OrdinalIgnoreCase));

            if (!alreadyThere)
            {
                dbContext.Tags.Add(Tag.Create(tenantId, builtIn.SpanishName, builtIn.ColorHex, builtIn.Category, builtInKey: builtIn.Key));
                created++;
            }

            dbContext.ProvisionedBuiltInTags.Add(ProvisionedBuiltInTag.Create(tenantId, builtIn.Key));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return created;
    }
}
