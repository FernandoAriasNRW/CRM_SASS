using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Domain.Entities;
using MediatR;

namespace Docs.Application.Annotations;

/// <summary>Las anotaciones de una página, resueltas incluidas: el panel las separa al pintar.</summary>
public sealed record GetPageAnnotationsQuery(Guid PageId) : IRequest<Result<List<AnnotationDto>>>;
