using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Domain.Entities;
using MediatR;

namespace Docs.Application.Annotations;

/// <summary>Marcar como resuelta, o volver a abrirla. Es el mismo comando con un booleano porque
/// son el mismo gesto en la pantalla: un interruptor, no dos acciones distintas.</summary>
public sealed record ResolveAnnotationCommand(Guid AnnotationId, Guid UserId, bool IsResolved)
    : IRequest<Result>;
