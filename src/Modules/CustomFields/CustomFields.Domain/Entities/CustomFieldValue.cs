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
    public string? Valor { get; private set; }

    private CustomFieldValue() { }

    public static CustomFieldValue Create(Guid tenantId, Guid definitionId, Guid entityId, string? valorCanonico)
    {
        if (definitionId == Guid.Empty || entityId == Guid.Empty)
            throw new InvalidOperationException("El valor necesita un campo y una entidad");

        var valor = new CustomFieldValue
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DefinitionId = definitionId,
            EntityId = entityId,
            Valor = valorCanonico
        };

        valor.RaiseDomainEvent(new CustomFieldValueSetEvent(valor.Id, tenantId, definitionId, entityId));

        return valor;
    }

    public void Cambiar(string? valorCanonico)
    {
        if (Valor == valorCanonico)
            return;

        Valor = valorCanonico;
        RaiseDomainEvent(new CustomFieldValueSetEvent(Id, TenantId, DefinitionId, EntityId));
    }
}
