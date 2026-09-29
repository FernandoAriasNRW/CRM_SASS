using BuildingBlocks.Domain.Primitives;
using Tags.Domain.ValueObjects;

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

    /// <summary>
    /// Quién la creó, que puede editarla y borrarla aunque no sea administrador. <c>null</c> en las
    /// que crea el sistema: las predefinidas, las de equipos y proyectos y las de la demostración.
    /// </summary>
    public Guid? CreatedBy { get; private set; }

    private Tag() { }

    public static Tag Create(
        Guid tenantId, string name, string colorHex, string category,
        Guid? externalReferenceId = null, string? builtInKey = null, Guid? createdBy = null)
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
            BuiltInKey = builtInKey,
            CreatedBy = createdBy
        };
    }

    /// <summary>Las de equipos y proyectos siguen a su equipo o proyecto: no se tocan a mano.</summary>
    public bool IsAutomatic => TagCategory.IsAutomatic(Category);

    public void Edit(string name, string colorHex, string category)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tag name is required");

        if (IsAutomatic || TagCategory.IsAutomatic(category))
            throw new InvalidOperationException("Las etiquetas de equipos y proyectos siguen a su equipo o proyecto y no se editan a mano");

        // Una predefinida renombrada o movida de categoría deja de serlo: si conservara la clave,
        // se seguiría mostrando el nombre del catálogo y no el que le han puesto.
        if (name != Name || category != Category)
            BuiltInKey = null;

        Name = name;
        ColorHex = colorHex;
        Category = category;
    }
}
