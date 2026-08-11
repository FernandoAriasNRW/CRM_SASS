using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using MediatR;

namespace Docs.Application.Queries;

public record DocumentDto(Guid Id, string Title, string Description, int Type, Guid OwnerId, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

public record GetDocumentsQuery(Guid TenantId, Guid UserId) : IRequest<Result<List<DocumentDto>>>;
