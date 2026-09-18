using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Domain.Entities;
using MediatR;

namespace Docs.Application.Handlers.Commands;

/// <summary>
/// Cambia una página de sitio: de padre, de orden, o las dos cosas.
///
/// <c>Page.ParentPageId</c> y <c>Page.Order</c> existían desde el primer día y sólo se escribían al
/// crear la página. No había forma de mover nada, así que el árbol de páginas era decorativo: cada
/// documento quedaba con la estructura exacta que le dejó la plantilla.
///
/// El orden se normaliza entero entre los hermanos después de mover. Guardar sólo el número que
/// manda el cliente deja huecos y empates —dos páginas con orden 3— y a partir de ahí el listado
/// se ordena por lo que decida la base, que no es estable.
/// </summary>
public record MovePageCommand(Guid PageId, Guid? ParentPageId, int Order) : IRequest<Result>, IAuthorizeEntity
{
    // La página no lleva el documento, así que se comprueba el nivel sobre los documentos en general.
    public string EntityType => "Document";
    public Guid EntityId => Guid.Empty;
    public string RequiredPermission => "Write";
}

public class MovePageHandler(IDocumentRepository repository)
    : IRequestHandler<MovePageCommand, Result>
{
    public async Task<Result> Handle(MovePageCommand request, CancellationToken cancellationToken)
    {
        var page = await repository.GetPageByIdAsync(request.PageId, cancellationToken);
        if (page is null)
            return Result.Failure("La página no existe.");

        if (request.ParentPageId == request.PageId)
            return Result.Failure("Una página no puede colgar de sí misma.");

        var documentPages = await repository.GetPagesByDocumentIdAsync(page.DocumentId, cancellationToken);
        var livePages = documentPages.Where(p => !p.IsDeleted).ToList();

        if (request.ParentPageId.HasValue)
        {
            var parent = livePages.FirstOrDefault(p => p.Id == request.ParentPageId.Value);
            if (parent is null)
                return Result.Failure("La página de destino no existe en este documento.");

            // Arrastrar una página encima de una de sus propias hijas dejaría a las dos fuera del
            // árbol: colgarían la una de la otra y ninguna del documento. Desaparecerían de la
            // barra lateral sin haberse borrado, que es peor que no dejar hacer el movimiento.
            if (IsDescendantOf(livePages, request.ParentPageId.Value, request.PageId))
                return Result.Failure("Una página no puede colgar de una de sus propias subpáginas.");
        }

        var previousParentId = page.ParentPageId;
        page.Move(request.ParentPageId);

        // Se renumera el destino entero: la que se mueve entra en la posición pedida y las demás
        // se recolocan a su alrededor.
        var target = livePages
            .Where(p => p.Id != page.Id && p.ParentPageId == request.ParentPageId)
            .OrderBy(p => p.Order)
            .ThenBy(p => p.CreatedAtUtc)
            .ToList();

        var position = Math.Clamp(request.Order, 0, target.Count);
        target.Insert(position, page);

        for (var i = 0; i < target.Count; i++)
            target[i].Reorder(i);

        // El origen también se renumera si la página cambió de padre: si no, deja un hueco en la
        // secuencia y el siguiente movimiento parte de números que ya no son consecutivos.
        //
        // Este comentario estaba escrito desde el principio sin el código que lo cumple: sacar la
        // primera de tres hermanas dejaba a las otras dos con orden 1 y 2, y soltar después una
        // página «en la posición 1» la ponía detrás de la que ya estaba primera.
        if (previousParentId != request.ParentPageId)
        {
            var origin = livePages
                .Where(p => p.Id != page.Id && p.ParentPageId == previousParentId)
                .OrderBy(p => p.Order)
                .ThenBy(p => p.CreatedAtUtc)
                .ToList();

            for (var i = 0; i < origin.Count; i++)
                origin[i].Reorder(i);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>Si <paramref name="candidate"/> cuelga, directa o indirectamente, de <paramref name="root"/>.</summary>
    private static bool IsDescendantOf(List<Page> pages, Guid candidate, Guid root)
    {
        var current = pages.FirstOrDefault(p => p.Id == candidate);

        // El tope por número de páginas es una red, no una regla: si un dato corrupto ya tuviera un
        // ciclo, recorrerlo sin límite colgaría la petición en vez de devolver un error.
        for (var hops = 0; current is not null && hops <= pages.Count; hops++)
        {
            if (current.Id == root) return true;
            if (current.ParentPageId is null) return false;
            current = pages.FirstOrDefault(p => p.Id == current.ParentPageId.Value);
        }

        return false;
    }
}
