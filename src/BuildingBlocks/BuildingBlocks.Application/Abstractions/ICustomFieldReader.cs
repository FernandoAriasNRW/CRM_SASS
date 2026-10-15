namespace BuildingBlocks.Application.Abstractions;

/// <summary>
/// Los campos personalizados de una organización, para quien los necesita sin ser CustomFields.
///
/// Lo usa el constructor de informes: «tickets por área» agrupa por un campo que ha creado la
/// organización, no por uno fijo. El catálogo pregunta qué campos hay y el motor, qué valor tiene
/// cada elemento. Lo implementa CustomFields, con el inquilino forzado porque los informes también
/// se generan en segundo plano.
/// </summary>
public interface ICustomFieldReader
{
    /// <summary>Los campos de un tipo de elemento («Task», «Ticket», «Project»), por nombre.</summary>
    Task<IReadOnlyList<CustomFieldInfo>> GetDefinitionsAsync(Guid tenantId, string entityType, CancellationToken ct = default);

    /// <summary>El valor de un campo en cada elemento que lo tenga. Los que no, no salen.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetValuesAsync(
        Guid tenantId, Guid definitionId, IReadOnlyCollection<Guid> entityIds, CancellationToken ct = default);

    /// <summary>
    /// El valor de un campo en todos los elementos de la organización que lo tienen. Para filtrar
    /// («solo los del área Ventas»): el motor no puede cruzar la consulta con otro módulo en SQL,
    /// así que evalúa el filtro sobre estos valores y filtra por los identificadores.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> GetAllValuesAsync(Guid tenantId, Guid definitionId, CancellationToken ct = default);
}

/// <summary>Un campo personalizado: lo justo para ofrecerlo y nombrarlo.</summary>
/// <param name="Options">Las opciones de un campo de selección; vacía en los demás.</param>
public sealed record CustomFieldInfo(Guid Id, string Name, string Type, string EntityType, IReadOnlyList<string> Options);
