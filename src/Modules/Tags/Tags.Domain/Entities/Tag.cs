using BuildingBlocks.Domain.Primitives;

namespace Tags.Domain.Entities;

public sealed class Tag : AggregateRoot, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string ColorHex { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public Guid? ExternalReferenceId { get; private set; } // Opcional, para enlazar con Id del Team o Project

    /// <summary>
    /// La clave de la etiqueta predefinida de la que sale («vip-client»), o <c>null</c> si la creó
    /// la organización. Con ella se muestra el nombre en el idioma de quien la mira; <c>Name</c>
    /// guarda el español, que es el idioma por defecto.
    /// </summary>
    public string? BuiltInKey { get; private set; }

    private Tag() { }

    public static Tag Create(Guid tenantId, string name, string colorHex, string category, Guid? externalReferenceId = null, string? builtInKey = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tag name is required");

        return new Tag
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            ColorHex = colorHex,
            Category = category,
            ExternalReferenceId = externalReferenceId,
            BuiltInKey = builtInKey
        };
    }

    public void Update(string name, string colorHex)
    {
        // Una predefinida renombrada deja de serlo: si conservara la clave, se seguiría mostrando
        // el nombre del catálogo y no el que le han puesto.
        if (!string.IsNullOrWhiteSpace(name) && name != Name)
        {
            Name = name;
            BuiltInKey = null;
        }
            
        if (!string.IsNullOrWhiteSpace(colorHex))
            ColorHex = colorHex;
    }
}
