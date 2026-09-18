using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Domain.Entities;
using MediatR;

namespace Docs.Application.Annotations;

public sealed class ResolveAnnotationHandler(IDocumentRepository repository)
    : IRequestHandler<ResolveAnnotationCommand, Result>
{
    public async Task<Result> Handle(ResolveAnnotationCommand request, CancellationToken cancellationToken)
    {
        var annotation = await repository.GetAnnotationAsync(request.AnnotationId, cancellationToken);
        if (annotation is null)
            return Result.Failure("La anotación no existe.");

        if (request.IsResolved) annotation.Resolve(request.UserId);
        else annotation.Reopen();

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
