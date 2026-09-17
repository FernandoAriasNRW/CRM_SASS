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
public record MoverPaginaCommand(Guid PageId, Guid? ParentPageId, int Order) : IRequest<Result>, IAuthorizeEntity
{
    // La página no lleva el documento, así que se comprueba el nivel sobre los documentos en general.
    public string EntityType => "Document";
    public Guid EntityId => Guid.Empty;
    public string RequiredPermission => "Write";
}

public class MoverPaginaHandler(IDocumentRepository repository)
    : IRequestHandler<MoverPaginaCommand, Result>
{
    public async Task<Result> Handle(MoverPaginaCommand request, CancellationToken cancellationToken)
    {
        var pagina = await repository.GetPageByIdAsync(request.PageId, cancellationToken);
        if (pagina is null)
            return Result.Failure("La página no existe.");

        if (request.ParentPageId == request.PageId)
            return Result.Failure("Una página no puede colgar de sí misma.");

        var hermanas = await repository.GetPagesByDocumentIdAsync(pagina.DocumentId, cancellationToken);
        var vivas = hermanas.Where(p => !p.IsDeleted).ToList();

        if (request.ParentPageId.HasValue)
        {
            var padre = vivas.FirstOrDefault(p => p.Id == request.ParentPageId.Value);
            if (padre is null)
                return Result.Failure("La página de destino no existe en este documento.");

            // Arrastrar una página encima de una de sus propias hijas dejaría a las dos fuera del
            // árbol: colgarían la una de la otra y ninguna del documento. Desaparecerían de la
            // barra lateral sin haberse borrado, que es peor que no dejar hacer el movimiento.
            if (EsDescendiente(vivas, request.ParentPageId.Value, request.PageId))
                return Result.Failure("Una página no puede colgar de una de sus propias subpáginas.");
        }

        pagina.Mover(request.ParentPageId);

        // Se renumera el destino entero: la que se mueve entra en la posición pedida y las demás
        // se recolocan a su alrededor.
        var destino = vivas
            .Where(p => p.Id != pagina.Id && p.ParentPageId == request.ParentPageId)
            .OrderBy(p => p.Order)
            .ThenBy(p => p.CreatedAtUtc)
            .ToList();

        var posicion = Math.Clamp(request.Order, 0, destino.Count);
        destino.Insert(posicion, pagina);

        for (var i = 0; i < destino.Count; i++)
            destino[i].Reordenar(i);

        // El origen también se renumera si la página cambió de padre: si no, deja un hueco en la
        // secuencia y el siguiente movimiento parte de números que ya no son consecutivos.
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>Si <paramref name="candidato"/> cuelga, directa o indirectamente, de <paramref name="raiz"/>.</summary>
    private static bool EsDescendiente(List<Page> todas, Guid candidato, Guid raiz)
    {
        var actual = todas.FirstOrDefault(p => p.Id == candidato);

        // El tope por número de páginas es una red, no una regla: si un dato corrupto ya tuviera un
        // ciclo, recorrerlo sin límite colgaría la petición en vez de devolver un error.
        for (var saltos = 0; actual is not null && saltos <= todas.Count; saltos++)
        {
            if (actual.Id == raiz) return true;
            if (actual.ParentPageId is null) return false;
            actual = todas.FirstOrDefault(p => p.Id == actual.ParentPageId.Value);
        }

        return false;
    }
}
