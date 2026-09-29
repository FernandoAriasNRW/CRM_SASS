using BuildingBlocks.Domain.Primitives;

namespace Tags.Domain.Entities;

/// <summary>
/// Una categoría que ha creado una organización, además de las de <see cref="ValueObjects.TagCategory"/>.
///
/// Las etiquetas la referencian por nombre (<c>Tags.Category</c>), igual que a las predefinidas:
/// así una etiqueta no necesita saber de qué clase es su categoría.
/// </summary>
public sealed class CustomTagCategory : AggregateRoot, ITenantEntity
{
    public const int MaxNameLength = 50;

    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    private CustomTagCategory() { }

    public static CustomTagCategory Create(Guid tenantId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Category name is required");

        return new CustomTagCategory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim()
        };
    }
}
