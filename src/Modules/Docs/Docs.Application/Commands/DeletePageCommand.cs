using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using MediatR;

namespace Docs.Application.Commands;

public record DeletePageCommand(Guid PageId) : IRequest<Result>;
