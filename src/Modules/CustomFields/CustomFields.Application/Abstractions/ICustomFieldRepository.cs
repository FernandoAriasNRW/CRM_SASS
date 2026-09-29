using BuildingBlocks.Domain;
using CustomFields.Domain.Entities;

namespace CustomFields.Application.Abstractions;

/// <summary>UnitOfWork propio del módulo, para que los handlers guarden en su DbContext.</summary>
public interface ICustomFieldsUnitOfWork : IUnitOfWork
{
}

public interface ICustomFieldRepository
{
  Task<CustomFieldDefinition?> GetDefinitionAsync(Guid tenantId, Guid id, CancellationToken ct = default);

  Task<IReadOnlyList<CustomFieldDefinition>> GetDefinitionsAsync(Guid tenantId, string? targetEntity, CancellationToken ct = default);

  /// <summary>Si ya hay un campo con ese nombre para la misma entidad. El nombre es lo que ve la gente.</summary>
  Task<bool> NameExistsAsync(Guid tenantId, string targetEntity, string name, Guid? excludingId, CancellationToken ct = default);

  Task AddDefinitionAsync(CustomFieldDefinition definition, CancellationToken ct = default);

  void RemoveDefinition(CustomFieldDefinition definition);

  Task<IReadOnlyList<CustomFieldValue>> GetValuesAsync(Guid tenantId, Guid entityId, CancellationToken ct = default);

  Task<CustomFieldValue?> GetValueAsync(Guid tenantId, Guid definitionId, Guid entityId, CancellationToken ct = default);

  Task AddValueAsync(CustomFieldValue value, CancellationToken ct = default);

  /// <summary>
  /// Borra los valores de una definición.
  ///
  /// Al quitar un campo hay que llevarse sus valores: dejarlos sería guardar respuestas a una
  /// pregunta que ya nadie hace, y ocuparían sitio sin que ninguna vista los muestre.
  /// </summary>
  Task RemoveValuesOfDefinitionAsync(Guid tenantId, Guid definitionId, CancellationToken ct = default);
}
