using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Comments.Domain.Mentions;

namespace Comments.Application;

/// <summary>«¿Qué comentarios hablan de esto?»: la vuelta de las menciones en comentarios.</summary>
public sealed record GetMentioningCommentsQuery(Guid TenantId, string Type, Guid EntityId)
    : IQuery<IReadOnlyList<MentioningCommentDto>>;

/// <summary>
/// Un comentario que menciona algo, con lo justo para enseñarlo y llevar a él: dónde está —el tipo
/// y el identificador de lo comentado—, quién lo escribió y un extracto con las menciones ya
/// convertidas en nombres.
/// </summary>
public sealed record MentioningCommentDto(
    Guid CommentId,
    string EntityType,
    Guid EntityId,
    Guid AuthorId,
    string Excerpt,
    DateTime CreatedAtUtc);

public sealed class GetMentioningCommentsHandler(ICommentRepository repository)
    : IQueryHandler<GetMentioningCommentsQuery, IReadOnlyList<MentioningCommentDto>>
{
    /// <summary>Cuántos se devuelven como mucho: es una lista para ojear, no un archivo.</summary>
    private const int Max = 50;

    /// <summary>Lo que cabe del texto en una línea del panel.</summary>
    private const int ExcerptLength = 160;

    public async Task<Result<IReadOnlyList<MentioningCommentDto>>> Handle(GetMentioningCommentsQuery request, CancellationToken ct)
    {
        var comments = await repository.GetMentioningAsync(request.TenantId, request.Type, request.EntityId, Max, ct);

        IReadOnlyList<MentioningCommentDto> result = comments
            .Select(c =>
            {
                var plain = CommentMentionReader.ToPlainText(c.Text);
                var excerpt = plain.Length <= ExcerptLength ? plain : plain[..ExcerptLength].TrimEnd() + "…";
                return new MentioningCommentDto(c.Id, c.EntityType, c.EntityId, c.AuthorId, excerpt, c.CreatedAtUtc);
            })
            .ToList();

        return Result<IReadOnlyList<MentioningCommentDto>>.Success(result);
    }
}
