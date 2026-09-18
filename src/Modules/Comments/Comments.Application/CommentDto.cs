using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Comments.Domain.Entities;

namespace Comments.Application;

/// <summary>
/// Un comentario tal como lo pinta la interfaz.
///
/// Lleva el nombre del autor resuelto porque el hilo lo necesita en cada línea, y pedirlo aparte
/// serían tantas consultas de usuario como comentarios. `Editado` se expone para poder decirlo:
/// un comentario que cambió sin avisar hace un hilo que no se puede leer con confianza.
/// </summary>
public sealed record CommentDto(
    Guid Id,
    Guid AuthorId,
    string Text,
    DateTime CreatedAtUtc,
    DateTime? EditedAtUtc,
    Guid? ReplyToId);
