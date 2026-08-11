using BuildingBlocks.Domain.Primitives;

namespace Docs.Domain.Entities;

public sealed class Page : Entity
{
    public Guid DocumentId { get; private set; }
    public Guid? ParentPageId { get; private set; }
    
    public string Title { get; private set; }
    public string Content { get; private set; } // Can be HTML, Markdown or JSON for TipTap
    public int Order { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public bool IsDeleted { get; private set; }

    private readonly List<Page> _subPages = new();
    public IReadOnlyCollection<Page> SubPages => _subPages.AsReadOnly();

    private Page() { }

    public static Page Create(Guid documentId, Guid? parentPageId, string title, string content, int order)
    {
        return new Page
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            ParentPageId = parentPageId,
            Title = title,
            Content = content,
            Order = order,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            IsDeleted = false
        };
    }

    public void UpdateContent(string title, string content)
    {
        Title = title;
        Content = content;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Delete()
    {
        IsDeleted = true;
    }
}
