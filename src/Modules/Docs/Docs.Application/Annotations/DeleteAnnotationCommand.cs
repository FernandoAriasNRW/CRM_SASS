using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Domain.Entities;
using MediatR;

namespace Docs.Application.Annotations;

public sealed record DeleteAnnotationCommand(Guid AnnotationId) : IRequest<Result>;
