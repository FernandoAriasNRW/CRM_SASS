using BuildingBlocks.Application.Abstractions;
using CustomFields.Domain.ValueObjects;
using CustomFields.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomFields.Infrastructure.Repositories;

/// <summary>
/// Responde <see cref="ICustomFieldReader"/>. Con el inquilino forzado y en el <c>Where</c>: lo
/// pregunta también el generador de informes, que corre sin petición.
/// </summary>
internal sealed class CustomFieldReader(CustomFieldsDbContext context) : ICustomFieldReader
{
    public async Task<IReadOnlyList<CustomFieldInfo>> GetDefinitionsAsync(Guid tenantId, string entityType, CancellationToken ct = default)
    {
        using var _ = context.AsTenant(tenantId);

        // Los calculados no se guardan: no hay valor por el que agrupar ni filtrar.
        var definitions = await context.Definitions.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.TargetEntity == entityType && d.Type != FieldType.Formula)
            .OrderBy(d => d.Name)
            .ToListAsync(ct);

        return definitions
            .Select(d => new CustomFieldInfo(d.Id, d.Name, d.Type, d.TargetEntity, d.Options.ToList()))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetValuesAsync(
        Guid tenantId, Guid definitionId, IReadOnlyCollection<Guid> entityIds, CancellationToken ct = default)
    {
        if (entityIds.Count == 0) return new Dictionary<Guid, string>();

        using var _ = context.AsTenant(tenantId);
        var ids = entityIds.Distinct().ToArray();

        var values = await context.Values.AsNoTracking()
            .Where(v => v.TenantId == tenantId && v.DefinitionId == definitionId && v.Value != null
                        && EF.Constant(ids).Contains(v.EntityId))
            .Select(v => new { v.EntityId, v.Value })
            .ToListAsync(ct);

        return values.ToDictionary(v => v.EntityId, v => v.Value!);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetAllValuesAsync(Guid tenantId, Guid definitionId, CancellationToken ct = default)
    {
        using var _ = context.AsTenant(tenantId);

        var values = await context.Values.AsNoTracking()
            .Where(v => v.TenantId == tenantId && v.DefinitionId == definitionId && v.Value != null)
            .Select(v => new { v.EntityId, v.Value })
            .ToListAsync(ct);

        return values.ToDictionary(v => v.EntityId, v => v.Value!);
    }
}
