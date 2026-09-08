using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Comments.Domain.Entities;

namespace Comments.Application;

// ---------------------------------------------------------------- Puertos

public interface ICommentRepository
{
    Task<Comment?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>El hilo de una entidad, del más antiguo al más nuevo: se lee en orden.</summary>
    Task<IReadOnlyList<Comment>> GetHiloAsync(
        Guid tenantId, string entidadDestino, Guid entityId, CancellationToken ct = default);

    Task AddAsync(Comment comentario, CancellationToken ct = default);
    Task UpdateAsync(Comment comentario, CancellationToken ct = default);
    Task RemoveAsync(Comment comentario, CancellationToken ct = default);
}

public interface ICommentsUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

// ---------------------------------------------------------------- DTO

/// <summary>
/// Un comentario tal como lo pinta la interfaz.
///
/// Lleva el nombre del autor resuelto porque el hilo lo necesita en cada línea, y pedirlo aparte
/// serían tantas consultas de usuario como comentarios. `Editado` se expone para poder decirlo:
/// un comentario que cambió sin avisar hace un hilo que no se puede leer con confianza.
/// </summary>
public sealed record CommentDto(
    Guid Id,
    Guid AutorId,
    string Texto,
    DateTime CreadoUtc,
    DateTime? EditadoUtc,
    Guid? RespondeAId);

// ---------------------------------------------------------------- Comandos y consultas

public sealed record GetCommentsQuery(
    Guid TenantId, string EntidadDestino, Guid EntityId) : IQuery<IReadOnlyList<CommentDto>>;

public sealed record AddCommentCommand(
    Guid TenantId,
    string EntidadDestino,
    Guid EntityId,
    Guid AutorId,
    string Texto,
    Guid? RespondeAId = null) : ICommand<CommentDto>;

public sealed record EditCommentCommand(
    Guid TenantId, Guid Id, Guid QuienEdita, string Texto) : ICommand<bool>;

public sealed record RemoveCommentCommand(
    Guid TenantId, Guid Id, Guid QuienBorra, string Rol) : ICommand<bool>;

// ---------------------------------------------------------------- Manejadores

public static class MapeoDeComentarios
{
    public static CommentDto ADto(Comment c) =>
        new(c.Id, c.AutorId, c.Texto, c.CreadoUtc, c.EditadoUtc, c.RespondeAId);
}

public sealed class GetCommentsHandler(ICommentRepository repositorio)
    : IQueryHandler<GetCommentsQuery, IReadOnlyList<CommentDto>>
{
    public async Task<Result<IReadOnlyList<CommentDto>>> Handle(GetCommentsQuery request, CancellationToken ct)
    {
        if (!TipoDeEntidadComentable.Existe(request.EntidadDestino))
            return Result<IReadOnlyList<CommentDto>>.Failure(Comment.Reglas.EntidadDesconocida);

        var hilo = await repositorio.GetHiloAsync(request.TenantId, request.EntidadDestino, request.EntityId, ct);

        return Result<IReadOnlyList<CommentDto>>.Success(
            hilo.Select(MapeoDeComentarios.ADto).ToList());
    }
}

public sealed class AddCommentHandler(
    ICommentRepository repositorio,
    ICommentsUnitOfWork unitOfWork) : ICommandHandler<AddCommentCommand, CommentDto>
{
    public async Task<Result<CommentDto>> Handle(AddCommentCommand request, CancellationToken ct)
    {
        // Las dos reglas del anidamiento hablan de otra fila, así que no las puede comprobar el
        // agregado: que a lo que se responde exista y no sea ya una respuesta, y que esté en el
        // mismo hilo. Sin la segunda, una respuesta podría colgarse de otra entidad y aparecer
        // en un hilo donde nadie la escribió.
        if (request.RespondeAId.HasValue)
        {
            var padre = await repositorio.GetByIdAsync(request.TenantId, request.RespondeAId.Value, ct);

            if (padre is null)
                return Result<CommentDto>.Failure(Comment.Reglas.NoEncontrado);

            if (padre.RespondeAId.HasValue)
                return Result<CommentDto>.Failure(Comment.Reglas.RespuestaDeRespuesta);

            if (padre.EntidadDestino != request.EntidadDestino || padre.EntityId != request.EntityId)
                return Result<CommentDto>.Failure(Comment.Reglas.RespondeAOtraEntidad);
        }

        Comment comentario;
        try
        {
            comentario = Comment.Create(
                request.TenantId, request.EntidadDestino, request.EntityId,
                request.AutorId, request.Texto, request.RespondeAId);
        }
        catch (InvalidOperationException ex) { return Result<CommentDto>.Failure(ex.Message); }

        await repositorio.AddAsync(comentario, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<CommentDto>.Success(MapeoDeComentarios.ADto(comentario));
    }
}

public sealed class EditCommentHandler(
    ICommentRepository repositorio,
    ICommentsUnitOfWork unitOfWork) : ICommandHandler<EditCommentCommand, bool>
{
    public async Task<Result<bool>> Handle(EditCommentCommand request, CancellationToken ct)
    {
        var comentario = await repositorio.GetByIdAsync(request.TenantId, request.Id, ct);
        if (comentario is null) return Result<bool>.Failure(Comment.Reglas.NoEncontrado);

        try { comentario.Editar(request.QuienEdita, request.Texto); }
        catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }

        await repositorio.UpdateAsync(comentario, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}

public sealed class RemoveCommentHandler(
    ICommentRepository repositorio,
    ICommentsUnitOfWork unitOfWork) : ICommandHandler<RemoveCommentCommand, bool>
{
    public async Task<Result<bool>> Handle(RemoveCommentCommand request, CancellationToken ct)
    {
        var comentario = await repositorio.GetByIdAsync(request.TenantId, request.Id, ct);
        if (comentario is null) return Result<bool>.Failure(Comment.Reglas.NoEncontrado);

        if (!comentario.LoPuedeBorrar(request.QuienBorra, request.Rol))
            return Result<bool>.Failure(Comment.Reglas.SoloElAutorOAdminBorra);

        await repositorio.RemoveAsync(comentario, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
