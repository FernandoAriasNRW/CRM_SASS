using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Domain.Entities;
using MediatR;

namespace Docs.Application.Anotaciones;

/// <summary>Una anotación tal como la lee la pantalla. El hilo se pide aparte, a Comments.</summary>
public sealed record AnotacionDto(
    Guid Id,
    Guid DocumentId,
    Guid PageId,
    string TextoCitado,
    Guid CreadaPor,
    DateTime CreadaUtc,
    DateTime? ResueltaUtc);

/// <summary>Las anotaciones de una página, resueltas incluidas: el panel las separa al pintar.</summary>
public sealed record GetAnotacionesQuery(Guid PageId) : IRequest<Result<List<AnotacionDto>>>;

public sealed record CrearAnotacionCommand(
    Guid TenantId, Guid DocumentId, Guid PageId, Guid CreadaPor, string TextoCitado)
    : IRequest<Result<Guid>>;

/// <summary>Marcar como resuelta, o volver a abrirla. Es el mismo comando con un booleano porque
/// son el mismo gesto en la pantalla: un interruptor, no dos acciones distintas.</summary>
public sealed record ResolverAnotacionCommand(Guid AnotacionId, Guid Quien, bool Resuelta)
    : IRequest<Result>;

public sealed record BorrarAnotacionCommand(Guid AnotacionId) : IRequest<Result>;

public sealed class GetAnotacionesHandler(IDocumentRepository repository)
    : IRequestHandler<GetAnotacionesQuery, Result<List<AnotacionDto>>>
{
    public async Task<Result<List<AnotacionDto>>> Handle(
        GetAnotacionesQuery request, CancellationToken cancellationToken)
    {
        var anotaciones = await repository.GetAnotacionesDePaginaAsync(request.PageId, cancellationToken);

        return Result<List<AnotacionDto>>.Success(anotaciones
            .Select(a => new AnotacionDto(
                a.Id, a.DocumentId, a.PageId, a.TextoCitado, a.CreadaPor, a.CreadaUtc, a.ResueltaUtc))
            .ToList());
    }
}

public sealed class CrearAnotacionHandler(IDocumentRepository repository)
    : IRequestHandler<CrearAnotacionCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CrearAnotacionCommand request, CancellationToken cancellationToken)
    {
        var pagina = await repository.GetPageByIdAsync(request.PageId, cancellationToken);
        if (pagina is null)
            return Result<Guid>.Failure("La página no existe.");

        // El documento sale de la página y no de lo que mande el cliente: si viniera de fuera,
        // una anotación podría quedar colgada de un documento que no es el suyo y el panel la
        // buscaría donde no está.
        var anotacion = AnotacionEnDocumento.Crear(
            request.TenantId, pagina.DocumentId, request.PageId, request.CreadaPor, request.TextoCitado);

        await repository.AddAnotacionAsync(anotacion, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(anotacion.Id);
    }
}

public sealed class ResolverAnotacionHandler(IDocumentRepository repository)
    : IRequestHandler<ResolverAnotacionCommand, Result>
{
    public async Task<Result> Handle(ResolverAnotacionCommand request, CancellationToken cancellationToken)
    {
        var anotacion = await repository.GetAnotacionAsync(request.AnotacionId, cancellationToken);
        if (anotacion is null)
            return Result.Failure("La anotación no existe.");

        if (request.Resuelta) anotacion.Resolver(request.Quien);
        else anotacion.Reabrir();

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class BorrarAnotacionHandler(IDocumentRepository repository)
    : IRequestHandler<BorrarAnotacionCommand, Result>
{
    public async Task<Result> Handle(BorrarAnotacionCommand request, CancellationToken cancellationToken)
    {
        var anotacion = await repository.GetAnotacionAsync(request.AnotacionId, cancellationToken);
        if (anotacion is null)
            return Result.Failure("La anotación no existe.");

        // Se borra de verdad, sin papelera.
        //
        // Una anotación no es contenido: es el clavo del que cuelga una conversación. Resolver ya
        // existe para «esto está atendido» y conserva el hilo; borrar es para «esto no debería
        // estar aquí», y guardar clavos invisibles sólo sirve para que el día de mañana alguien
        // los liste sin querer. Los comentarios del hilo se quedan en su módulo: quien los
        // escribió sigue siendo dueño de borrarlos.
        await repository.RemoveAnotacionAsync(anotacion, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
