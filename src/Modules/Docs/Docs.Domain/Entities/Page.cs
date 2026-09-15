using BuildingBlocks.Domain.Primitives;

namespace Docs.Domain.Entities;

public sealed class Page : Entity, ISoftDeletable
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

    private Page() { Content = null!; Title = null!; } // EF las rellena al materializar.

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

    /// <summary>Cambia el título sin tocar el contenido, para renombrar desde el árbol.</summary>
    public void Renombrar(string title)
    {
        Title = title;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Cuelga la página de otra, o del documento cuando el padre es <c>null</c>.</summary>
    public void Mover(Guid? parentPageId)
    {
        ParentPageId = parentPageId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Coloca la página entre sus hermanas.
    ///
    /// No toca <c>UpdatedAtUtc</c>: reordenar la barra lateral no es editar el documento, y si lo
    /// marcara, mover una página movería también su sitio en «recientes».
    /// </summary>
    public void Reordenar(int order) => Order = order;

    public void Delete()
    {
        IsDeleted = true;
    }
}
