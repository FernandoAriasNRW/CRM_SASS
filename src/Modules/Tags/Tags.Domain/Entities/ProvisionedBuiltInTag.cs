using BuildingBlocks.Domain.Primitives;

namespace Tags.Domain.Entities;

/// <summary>
/// Constancia de que una organización ya recibió una etiqueta predefinida.
///
/// <b>Existe porque las etiquetas se pueden borrar y renombrar.</b> El aprovisionamiento corre en
/// cada arranque; si decidiera qué falta mirando las etiquetas, una predefinida borrada o
/// renombrada volvería a aparecer al día siguiente. Mirando esta tabla, lo que la organización
/// quitó se queda quitado, y una predefinida que se añada al catálogo en una versión futura sí
/// llega a las organizaciones que ya existían.
/// </summary>
public sealed class ProvisionedBuiltInTag : ITenantEntity
{
    public Guid TenantId { get; private set; }
    public string BuiltInKey { get; private set; } = string.Empty;

    private ProvisionedBuiltInTag() { }

    public static ProvisionedBuiltInTag Create(Guid tenantId, string builtInKey)
        => new() { TenantId = tenantId, BuiltInKey = builtInKey };
}
