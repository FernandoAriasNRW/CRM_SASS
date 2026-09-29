using CustomFields.Application.Abstractions;
using CustomFields.Domain.Entities;
using CustomFields.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomFields.Infrastructure.Repositories;

public sealed class EfCustomFieldRepository(CustomFieldsDbContext context) : ICustomFieldRepository
{
  public async Task<CustomFieldDefinition?> GetDefinitionAsync(Guid tenantId, Guid id, CancellationToken ct = default)
      => await context.Definitions.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == id, ct);

  public async Task<IReadOnlyList<CustomFieldDefinition>> GetDefinitionsAsync(Guid tenantId, string? targetEntity, CancellationToken ct = default)
  {
    var query = context.Definitions.AsNoTracking().Where(d => d.TenantId == tenantId);

    if (!string.IsNullOrWhiteSpace(targetEntity))
      query = query.Where(d => d.TargetEntity == targetEntity);

    return await query.OrderBy(d => d.Position).ThenBy(d => d.Name).ToListAsync(ct);
  }

  public async Task<bool> NameExistsAsync(Guid tenantId, string targetEntity, string name, Guid? excludingId, CancellationToken ct = default)
      => await context.Definitions.AnyAsync(
          d => d.TenantId == tenantId
               && d.TargetEntity == targetEntity
               && d.Name == name
               && (excludingId == null || d.Id != excludingId), ct);

  public async Task AddDefinitionAsync(CustomFieldDefinition definition, CancellationToken ct = default)
      => await context.Definitions.AddAsync(definition, ct);

  public void RemoveDefinition(CustomFieldDefinition definition)
      => context.Definitions.Remove(definition);

  public async Task<IReadOnlyList<CustomFieldValue>> GetValuesAsync(Guid tenantId, Guid entityId, CancellationToken ct = default)
      => await context.Values.AsNoTracking()
          .Where(v => v.TenantId == tenantId && v.EntityId == entityId)
          .ToListAsync(ct);

  public async Task<CustomFieldValue?> GetValueAsync(Guid tenantId, Guid definitionId, Guid entityId, CancellationToken ct = default)
      => await context.Values.FirstOrDefaultAsync(
          v => v.TenantId == tenantId && v.DefinitionId == definitionId && v.EntityId == entityId, ct);

  public async Task AddValueAsync(CustomFieldValue value, CancellationToken ct = default)
      => await context.Values.AddAsync(value, ct);

  public async Task RemoveValuesOfDefinitionAsync(Guid tenantId, Guid definitionId, CancellationToken ct = default)
  {
    var values = await context.Values
        .Where(v => v.TenantId == tenantId && v.DefinitionId == definitionId)
        .ToListAsync(ct);

    context.Values.RemoveRange(values);
  }
}
