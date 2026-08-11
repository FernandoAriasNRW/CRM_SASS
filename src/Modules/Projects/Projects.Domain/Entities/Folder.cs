using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;

namespace Projects.Domain.Entities;

public sealed class Folder : AggregateRoot
{
    public Guid TenantId { get; private set; }
    public Guid SpaceId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    // Soft Delete
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }

    private Folder() { }

    public static Folder Create(Guid tenantId, Guid spaceId, string name)
    {
        return new Folder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SpaceId = spaceId,
            Name = name,
            IsDeleted = false
        };
    }

    public void Update(string name)
    {
        Name = name;
    }

    public void Delete(Guid deletedBy)
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        DeletedBy = deletedBy;
    }
}
