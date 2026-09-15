using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using MediatR;

namespace Docs.Application.Commands;

public record CreateFromTemplateCommand(
    Guid TenantId,
    Guid OwnerId,
    string? TemplateKey,
    Guid? TemplateDocumentId,
    string? CustomTitle = null,
    /// <summary>
    /// «es» o «en». Decide el idioma del contenido de las plantillas del sistema; las propias se
    /// copian tal como las escribió el equipo. Sin valor, español, que es el idioma de origen.
    /// </summary>
    string? Idioma = null) : IRequest<Result<Guid>>;
