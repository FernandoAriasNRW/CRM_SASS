using BuildingBlocks.Domain.Primitives;

namespace Tags.Domain.Entities;

public sealed class Tag : AggregateRoot
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string ColorHex { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public Guid? ExternalReferenceId { get; private set; } // Opcional, para enlazar con Id del Team o Project

    private Tag() { }

    public static Tag Create(Guid tenantId, string name, string colorHex, string category, Guid? externalReferenceId = null)
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
            ExternalReferenceId = externalReferenceId
        };
    }

    public void Update(string name, string colorHex)
    {
        if (!string.IsNullOrWhiteSpace(name))
            Name = name;
            
        if (!string.IsNullOrWhiteSpace(colorHex))
            ColorHex = colorHex;
    }
}
