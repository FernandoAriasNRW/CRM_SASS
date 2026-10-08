using BuildingBlocks.Domain.Primitives;

namespace Docs.Domain.Entities;

/// <summary>
/// Una página de un documento.
///
/// Lleva su propio <c>TenantId</c> aunque ya lo tenga su documento: las páginas se buscan por su
/// identificador, sin pasar por el documento, y sin inquilino propio el filtro global no las
/// alcanzaba. Quien supiera el identificador de una página de otra organización podía leerla,
/// editarla, moverla o borrarla.
/// </summary>
public sealed class Page : Entity, ITenantEntity, ISoftDeletable
{
    public Guid TenantId { get; private set; }
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

    public static Page Create(DateTime nowUtc, Guid tenantId, Guid documentId, Guid? parentPageId, string title, string content, int order)
    {
        return new Page
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentId = documentId,
            ParentPageId = parentPageId,
            Title = title,
            Content = content,
            Order = order,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            IsDeleted = false
        };
    }

    public void UpdateContent(DateTime nowUtc, string title, string content)
    {
        Title = title;
        Content = content;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Cambia el título sin tocar el contenido, para renombrar desde el árbol.</summary>
    public void Rename(DateTime nowUtc, string title)
    {
        Title = title;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Cuelga la página de otra, o del documento cuando el padre es <c>null</c>.</summary>
    public void Move(DateTime nowUtc, Guid? parentPageId)
    {
        ParentPageId = parentPageId;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Coloca la página entre sus hermanas.
    ///
    /// No toca <c>UpdatedAtUtc</c>: reordenar la barra lateral no es editar el documento, y si lo
    /// marcara, mover una página movería también su sitio en «recientes».
    /// </summary>
    public void Reorder(int order) => Order = order;

    public void Delete()
    {
        IsDeleted = true;
    }
}
