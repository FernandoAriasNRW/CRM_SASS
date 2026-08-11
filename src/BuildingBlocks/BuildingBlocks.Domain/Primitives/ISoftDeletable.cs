namespace BuildingBlocks.Domain.Primitives;

/// <summary>
/// Marca una entidad que se borra lógicamente en lugar de físicamente.
///
/// Existe para que el filtro global pueda componerse en una sola expresión junto al de
/// tenant. EF Core no acumula filtros: cada llamada a <c>HasQueryFilter</c> reemplaza
/// la anterior, así que declarar el soft delete por separado desactivaría el
/// aislamiento por tenant sin aviso alguno.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; }
}
