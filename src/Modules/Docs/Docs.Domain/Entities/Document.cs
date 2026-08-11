using BuildingBlocks.Domain.Primitives;

namespace Docs.Domain.Entities;

public sealed class Document : Entity, ITenantEntity, ISoftDeletable
{
    public Guid TenantId { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public ValueObjects.DocumentType Type { get; private set; }
    
    // Ownership
    public Guid OwnerId { get; private set; }
    public Guid? TeamId { get; private set; }
    public Guid? ProjectId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private readonly List<Page> _pages = new();
    public IReadOnlyCollection<Page> Pages => _pages.AsReadOnly();

    private readonly List<DocumentPermission> _permissions = new();
    public IReadOnlyCollection<DocumentPermission> Permissions => _permissions.AsReadOnly();

    private Document() { Description = null!; Title = null!; } // EF las rellena al materializar.

    public static Document Create(Guid tenantId, string title, string description, ValueObjects.DocumentType type, Guid ownerId, Guid? teamId, Guid? projectId)
    {
        return new Document
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title,
            Description = description,
            Type = type,
            OwnerId = ownerId,
            TeamId = teamId,
            ProjectId = projectId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            IsDeleted = false
        };
    }

    public void Update(string title, string description)
    {
        Title = title;
        Description = description;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void AddPage(Page page)
    {
        _pages.Add(page);
    }

    public void AddPermission(DocumentPermission permission)
    {
        _permissions.Add(permission);
    }

    public void Delete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }
}
