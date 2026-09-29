using Microsoft.EntityFrameworkCore;
using Tags.Application.Abstractions;
using Tags.Application.BuiltIn;
using Tags.Domain.Entities;
using Tags.Infrastructure.Persistence;

namespace Tags.Infrastructure;

internal sealed class BuiltInTagProvisioner(TagsDbContext dbContext) : IBuiltInTagProvisioner
{
    public async Task<int> ProvisionAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        // Fuera de una petición el filtro compara contra Guid.Empty y la tabla se ve vacía: sin
        // fijar el inquilino, cada arranque volvería a crear todas las predefinidas.
        using var _ = dbContext.AsTenant(tenantId);

        var existing = await dbContext.Tags
            .Where(t => t.TenantId == tenantId)
            .Select(t => new { t.BuiltInKey, t.Category, t.Name })
            .ToListAsync(cancellationToken);

        var existingKeys = existing.Where(t => t.BuiltInKey != null).Select(t => t.BuiltInKey!).ToHashSet();

        // Por clave, y además por nombre: si la organización ya tiene a mano una etiqueta con el
        // nombre de una predefinida en la misma categoría, crearla chocaría con el índice único.
        var missing = BuiltInTags.All
            .Where(b => !existingKeys.Contains(b.Key))
            .Where(b => !existing.Any(t => t.Category == b.Category
                && string.Equals(t.Name, b.SpanishName, StringComparison.OrdinalIgnoreCase)))
            .Select(b => Tag.Create(tenantId, b.SpanishName, b.ColorHex, b.Category, builtInKey: b.Key))
            .ToList();

        if (missing.Count == 0)
            return 0;

        dbContext.Tags.AddRange(missing);
        await dbContext.SaveChangesAsync(cancellationToken);
        return missing.Count;
    }
}
