using CustomFields.Application.Abstractions;
using CustomFields.Domain.Entities;
using CustomFields.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomFields.Infrastructure.Repositories;

public sealed class EfCustomFieldRepository(CustomFieldsDbContext context) : ICustomFieldRepository
{
  public async Task<CustomFieldDefinition?> GetDefinitionAsync(Guid tenantId, Guid id, CancellationToken ct = default)
      => await context.Definitions.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == id, ct);

  public async Task<IReadOnlyList<CustomFieldDefinition>> GetDefinitionsAsync(Guid tenantId, string? entidadDestino, CancellationToken ct = default)
  {
    var consulta = context.Definitions.AsNoTracking().Where(d => d.TenantId == tenantId);

    if (!string.IsNullOrWhiteSpace(entidadDestino))
      consulta = consulta.Where(d => d.EntidadDestino == entidadDestino);

    return await consulta.OrderBy(d => d.Posicion).ThenBy(d => d.Nombre).ToListAsync(ct);
  }

  public async Task<bool> ExisteNombreAsync(Guid tenantId, string entidadDestino, string nombre, Guid? excluyendo, CancellationToken ct = default)
      => await context.Definitions.AnyAsync(
          d => d.TenantId == tenantId
               && d.EntidadDestino == entidadDestino
               && d.Nombre == nombre
               && (excluyendo == null || d.Id != excluyendo), ct);

  public async Task AddDefinitionAsync(CustomFieldDefinition definicion, CancellationToken ct = default)
      => await context.Definitions.AddAsync(definicion, ct);

  public void RemoveDefinition(CustomFieldDefinition definicion)
      => context.Definitions.Remove(definicion);

  public async Task<IReadOnlyList<CustomFieldValue>> GetValuesAsync(Guid tenantId, Guid entityId, CancellationToken ct = default)
      => await context.Values.AsNoTracking()
          .Where(v => v.TenantId == tenantId && v.EntityId == entityId)
          .ToListAsync(ct);

  public async Task<CustomFieldValue?> GetValueAsync(Guid tenantId, Guid definitionId, Guid entityId, CancellationToken ct = default)
      => await context.Values.FirstOrDefaultAsync(
          v => v.TenantId == tenantId && v.DefinitionId == definitionId && v.EntityId == entityId, ct);

  public async Task AddValueAsync(CustomFieldValue valor, CancellationToken ct = default)
      => await context.Values.AddAsync(valor, ct);

  public async Task RemoveValuesOfDefinitionAsync(Guid tenantId, Guid definitionId, CancellationToken ct = default)
  {
    var valores = await context.Values
        .Where(v => v.TenantId == tenantId && v.DefinitionId == definitionId)
        .ToListAsync(ct);

    context.Values.RemoveRange(valores);
  }
}
