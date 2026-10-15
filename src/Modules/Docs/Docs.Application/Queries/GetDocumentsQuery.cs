using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using MediatR;

namespace Docs.Application.Queries;

public record DocumentDto(Guid Id, string Title, string Description, int Type, Guid OwnerId, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

/// <param name="Scope">El filtro del menú lateral, resuelto. Sin él, la lista de siempre.</param>
public record GetDocumentsQuery(Guid TenantId, Guid UserId, BuildingBlocks.Application.ViewScope? Scope = null) : IRequest<Result<List<DocumentDto>>>;
