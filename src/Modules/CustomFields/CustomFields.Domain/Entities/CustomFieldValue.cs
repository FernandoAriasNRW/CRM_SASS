using BuildingBlocks.Domain.Primitives;
using CustomFields.Domain.Events;

namespace CustomFields.Domain.Entities;

/// <summary>
/// El valor de un campo personalizado para una entidad concreta.
///
/// Se guarda como texto en forma canónica, no en una columna por tipo. Una tabla con
/// `ValorTexto`, `ValorNumero`, `ValorFecha`… tendría casi todas las columnas a nulo en cada
/// fila y crecería con cada tipo nuevo. El texto canónico —números con punto, fechas ISO— es
/// ordenable y comparable, que es lo que se necesita para filtrar y agrupar.
/// </summary>
public sealed class CustomFieldValue : AggregateRoot, ITenantEntity
{
    public Guid TenantId { get; private set; }

    public Guid DefinitionId { get; private set; }

    /// <summary>La tarea o el proyecto al que pertenece el valor.</summary>
    public Guid EntityId { get; private set; }

    /// <summary>Ya validado y en forma canónica. Nulo significa «sin valor».</summary>
    public string? Value { get; private set; }

    private CustomFieldValue() { }

    public static CustomFieldValue Create(Guid tenantId, Guid definitionId, Guid entityId, string? canonicalValue)
    {
        if (definitionId == Guid.Empty || entityId == Guid.Empty)
            throw new InvalidOperationException("El valor necesita un campo y una entidad");

        var value = new CustomFieldValue
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DefinitionId = definitionId,
            EntityId = entityId,
            Value = canonicalValue
        };

        value.RaiseDomainEvent(new CustomFieldValueSetEvent(value.Id, tenantId, definitionId, entityId));

        return value;
    }

    public void Change(string? canonicalValue)
    {
        if (Value == canonicalValue)
            return;

        Value = canonicalValue;
        RaiseDomainEvent(new CustomFieldValueSetEvent(Id, TenantId, DefinitionId, EntityId));
    }
}
