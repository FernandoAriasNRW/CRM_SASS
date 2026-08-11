using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using MediatR;

namespace Docs.Application.Commands;

public record DeleteDocumentCommand(Guid DocumentId) : IRequest<Result>;
