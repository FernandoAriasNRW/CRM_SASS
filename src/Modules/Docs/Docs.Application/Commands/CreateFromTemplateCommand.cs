using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using MediatR;

namespace Docs.Application.Commands;

public record CreateFromTemplateCommand(
    Guid TenantId,
    Guid OwnerId,
    string? TemplateKey,
    Guid? TemplateDocumentId,
    string? CustomTitle = null) : IRequest<Result<Guid>>;
