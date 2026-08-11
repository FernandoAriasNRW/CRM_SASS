using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;

namespace Projects.Domain.Entities;

public sealed class Space : AggregateRoot
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Color { get; private set; } = string.Empty;

    // Soft Delete
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }

    private Space() { }

    public static Space Create(Guid tenantId, string name, string description, string color)
    {
        return new Space
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Description = description,
            Color = color,
            IsDeleted = false
        };
    }

    public void Update(string name, string description, string color)
    {
        Name = name;
        Description = description;
        Color = color;
    }

    public void Delete(Guid deletedBy)
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        DeletedBy = deletedBy;
    }
}
