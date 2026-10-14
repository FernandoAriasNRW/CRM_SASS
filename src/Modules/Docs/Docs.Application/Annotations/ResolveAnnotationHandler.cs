using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using Docs.Application.Authorization;
using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Domain.Entities;
using MediatR;

namespace Docs.Application.Annotations;

/// <summary>Resolver o reabrir pide poder leer el documento, como comentarlo.</summary>
public sealed class ResolveAnnotationHandler(
    TimeProvider timeProvider, IDocumentRepository repository,
    IUserContext user, IEntityPermissionService permissions)
    : IRequestHandler<ResolveAnnotationCommand, Result>
{
    public async Task<Result> Handle(ResolveAnnotationCommand request, CancellationToken cancellationToken)
    {
        var annotation = await repository.GetAnnotationAsync(request.AnnotationId, cancellationToken);
        if (annotation is null)
            return Result.Failure("La anotación no existe.");

        await permissions.EnsureOnDocumentAsync(user, annotation.DocumentId, "Read", cancellationToken);

        if (request.IsResolved) annotation.Resolve(timeProvider.GetUtcNow().UtcDateTime, request.UserId);
        else annotation.Reopen();

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
