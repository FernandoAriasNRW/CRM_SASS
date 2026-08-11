using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using MediatR;

namespace Docs.Application.Commands;

public record SaveAsTemplateCommand(
    Guid TenantId,
    Guid OwnerId,
    Guid DocumentId,
    string? CustomTitle = null,
    string? Description = null) : IRequest<Result<Guid>>;
