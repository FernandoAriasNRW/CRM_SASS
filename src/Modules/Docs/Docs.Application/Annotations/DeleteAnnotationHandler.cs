using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Domain.Entities;
using MediatR;

namespace Docs.Application.Annotations;

public sealed class DeleteAnnotationHandler(IDocumentRepository repository)
    : IRequestHandler<DeleteAnnotationCommand, Result>
{
    public async Task<Result> Handle(DeleteAnnotationCommand request, CancellationToken cancellationToken)
    {
        var annotation = await repository.GetAnnotationAsync(request.AnnotationId, cancellationToken);
        if (annotation is null)
            return Result.Failure("La anotación no existe.");

        // Se borra de verdad, sin papelera.
        //
        // Una anotación no es contenido: es el clavo del que cuelga una conversación. Resolver ya
        // existe para «esto está atendido» y conserva el hilo; borrar es para «esto no debería
        // estar aquí», y guardar clavos invisibles sólo sirve para que el día de mañana alguien
        // los liste sin querer. Los comentarios del hilo se quedan en su módulo: quien los
        // escribió sigue siendo dueño de borrarlos.
        await repository.RemoveAnnotationAsync(annotation, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
