using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using MediatR;

namespace Docs.Application.Commands;

public record CreateDocumentCommand(
    Guid TenantId,
    Guid OwnerId,
    string Title,
    string Description,
    int Type,
    Guid? TeamId,
    Guid? ProjectId,
    string? InitialContent = null) : IRequest<Result<Guid>>;
