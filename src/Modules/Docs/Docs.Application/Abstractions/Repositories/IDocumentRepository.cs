using Docs.Domain.Entities;

namespace Docs.Application.Abstractions.Repositories;

public interface IDocumentRepository
{
    Task AddAsync(Document document, CancellationToken cancellationToken = default);
    Task<List<Document>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Page?> GetPageByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddPageAsync(Page page, CancellationToken cancellationToken = default);
    Task<List<Page>> GetPagesByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default);
    /// <summary>Suma uno al contador de la plantilla, creando la fila si es la primera vez.</summary>
    Task RegistrarUsoDePlantillaAsync(Guid tenantId, string clave, CancellationToken cancellationToken = default);

    /// <summary>Cuánto se ha usado cada plantilla en este inquilino.</summary>
    Task<List<UsoDePlantilla>> GetUsosDePlantillaAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Las anotaciones pegadas a una página, resueltas incluidas.</summary>
    Task<List<AnotacionEnDocumento>> GetAnotacionesDePaginaAsync(Guid pageId, CancellationToken cancellationToken = default);

    Task<AnotacionEnDocumento?> GetAnotacionAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAnotacionAsync(AnotacionEnDocumento anotacion, CancellationToken cancellationToken = default);

    Task RemoveAnotacionAsync(AnotacionEnDocumento anotacion, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
