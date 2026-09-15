using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Application.Commands;
using Docs.Domain.Entities;
using Docs.Domain.ValueObjects;
using MediatR;

namespace Docs.Application.Handlers.Commands;

public class CreateFromTemplateHandler(IDocumentRepository repository) 
    : IRequestHandler<CreateFromTemplateCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateFromTemplateCommand request, CancellationToken cancellationToken)
    {
        // La misma clave que usa la galería para pedir la plantilla es la que se cuenta: la del
        // sistema tal cual, o el identificador del documento plantilla en texto.
        var claveDeLaPlantilla = request.TemplateDocumentId.HasValue && request.TemplateDocumentId.Value != Guid.Empty
            ? request.TemplateDocumentId.Value.ToString()
            : (request.TemplateKey ?? "").ToLowerInvariant();

        string docTitle;
        string docDescription = "";
        DocumentType docType = DocumentType.List;
        List<(string PageTitle, string Content)> templatePages = new();

        if (request.TemplateDocumentId.HasValue && request.TemplateDocumentId.Value != Guid.Empty)
        {
            var customTemplate = await repository.GetByIdAsync(request.TemplateDocumentId.Value, cancellationToken);
            if (customTemplate == null)
                return Result<Guid>.Failure("Custom template document not found");

            docTitle = !string.IsNullOrWhiteSpace(request.CustomTitle) ? request.CustomTitle : customTemplate.Title;
            docDescription = customTemplate.Description;
            docType = DocumentType.List;

            var pages = await repository.GetPagesByDocumentIdAsync(customTemplate.Id, cancellationToken);
            foreach (var p in pages.Where(page => !page.IsDeleted))
            {
                templatePages.Add((p.Title, p.Content));
            }
        }
        else
        {
            var contenido = Plantillas.PlantillasPredefinidas.Para(
                request.TemplateKey ?? string.Empty, request.Idioma, DateTime.UtcNow);

            // Una clave que no existe es un error, no un documento en blanco. Antes caía en un
            // `default` que creaba «Untitled Document» y le contaba un uso a una plantilla
            // inexistente: la petición respondía bien haciendo otra cosa.
            if (contenido is null)
                return Result<Guid>.Failure($"No existe la plantilla «{request.TemplateKey}».");

            docTitle = !string.IsNullOrWhiteSpace(request.CustomTitle) ? request.CustomTitle : contenido.Titulo;
            docDescription = contenido.Descripcion;
            docType = contenido.Tipo;
            templatePages.AddRange(contenido.Paginas);
        }

        var document = Document.Create(
            request.TenantId,
            docTitle,
            docDescription,
            docType,
            request.OwnerId,
            null,
            null);

        var permission = DocumentPermission.CreateForUser(document.Id, request.OwnerId, true, true, true);
        document.AddPermission(permission);

        await repository.AddAsync(document, cancellationToken);

        int order = 0;
        foreach (var pageData in templatePages)
        {
            var page = Page.Create(document.Id, null, pageData.PageTitle, pageData.Content, order++);
            await repository.AddPageAsync(page, cancellationToken);
        }

        // Se apunta el uso en la misma transacción que la creación. Contarlo desde el cliente
        // dejaría el contador a merced de una pestaña que se cierra a media petición: la galería
        // ordenaría por «veces que alguien pulsó», no por «documentos que salieron de aquí».
        await repository.RegistrarUsoDePlantillaAsync(request.TenantId, claveDeLaPlantilla, cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(document.Id);
    }
}
