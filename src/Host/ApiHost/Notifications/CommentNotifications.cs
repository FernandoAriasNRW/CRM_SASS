using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Events;
using Comments.Domain.Events;
using Comments.Domain.Mentions;
using Comments.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Notifications.Application.Sending;
using Notifications.Domain.Entities;
using EntityTypes = BuildingBlocks.Domain.EntityTypes;

namespace ApiHost.Notifications;

/// <summary>
/// Los avisos de los comentarios: a los interesados de lo comentado, y a quien se menciona.
///
/// <b>Una mención no avisa dos veces.</b> Quien está mencionado recibe «te han mencionado» y no
/// además «han comentado en tu tarea»: es el mismo comentario, y dos avisos por uno enseñan a
/// ignorarlos. Una mención a un equipo llega a sus miembros.
/// </summary>
public sealed class CommentNotifications(
    INotificationSender sender,
    InterestedParties parties,
    CommentsDbContext comments,
    ITeamDirectory teams)
    : INotificationHandler<DomainEventNotification<CommentAddedEvent>>,
      INotificationHandler<DomainEventNotification<CommentMentionsAddedEvent>>
{
    /// <summary>Lo que cabe del comentario en el cuerpo de un aviso.</summary>
    private const int ExcerptLength = 160;

    public async Task Handle(DomainEventNotification<CommentAddedEvent> notification, CancellationToken ct)
    {
        var e = notification.DomainEvent;
        var kind = e.EntityType switch
        {
            EntityTypes.Task => NotificationCatalog.TaskCommented,
            EntityTypes.Ticket => NotificationCatalog.TicketCommented,
            EntityTypes.Project => NotificationCatalog.ProjectCommented,
            _ => null,
        };
        if (kind is null) return;

        var people = await parties.OfAsync(e.TenantId, e.EntityType, e.EntityId, ct);
        var comment = await LoadAsync(e.TenantId, e.CommentId, ct);
        if (people is null || comment is null) return;

        var mentioned = await MentionedPeopleAsync(e.TenantId, comment.Mentions, ct);
        var recipients = people.Everyone.Where(id => !mentioned.Contains(id));

        await sender.SendAsync(new NotificationMessage(
            e.TenantId, kind,
            $"Nuevo comentario en «{people.Title}»",
            Excerpt(comment.Text), e.AuthorId, e.EntityType, e.EntityId), recipients, ct);
    }

    public async Task Handle(DomainEventNotification<CommentMentionsAddedEvent> notification, CancellationToken ct)
    {
        var e = notification.DomainEvent;
        var comment = await LoadAsync(e.TenantId, e.CommentId, ct);
        if (comment is null) return;

        var mentioned = await MentionedPeopleAsync(e.TenantId,
            e.Mentions.Select(m => new CommentMention(m.Type, m.EntityId, string.Empty)).ToList(), ct);
        if (mentioned.Count == 0) return;

        // Lleva a lo comentado, si es algo con ficha: una tarea, un ticket o un proyecto.
        var linkable = e.EntityType is EntityTypes.Task or EntityTypes.Ticket or EntityTypes.Project;

        await sender.SendAsync(new NotificationMessage(
            e.TenantId, NotificationCatalog.Mention,
            "Te han mencionado en un comentario",
            Excerpt(comment.Text), e.AuthorId,
            linkable ? e.EntityType : null, linkable ? e.EntityId : null), mentioned, ct);
    }

    private sealed record LoadedComment(string Text, IReadOnlyCollection<CommentMention> Mentions);

    private async Task<LoadedComment?> LoadAsync(Guid tenantId, Guid commentId, CancellationToken ct)
    {
        var comment = await comments.Comments.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == commentId, ct);
        return comment is null ? null : new LoadedComment(comment.Text, comment.Mentions);
    }

    /// <summary>Las personas mencionadas, contando a los miembros de los equipos mencionados.</summary>
    private async Task<HashSet<Guid>> MentionedPeopleAsync(Guid tenantId, IEnumerable<CommentMention> mentions, CancellationToken ct)
    {
        var people = new HashSet<Guid>();
        foreach (var mention in mentions)
        {
            if (mention.Type == MentionTypes.Person)
                people.Add(mention.EntityId);
            else if (mention.Type == MentionTypes.Team && await teams.GetMemberIdsAsync(tenantId, mention.EntityId, ct) is { } members)
                people.UnionWith(members);
        }
        return people;
    }

    private static string Excerpt(string text)
    {
        var plain = CommentMentionReader.ToPlainText(text);
        return plain.Length <= ExcerptLength ? plain : plain[..ExcerptLength].TrimEnd() + "…";
    }
}
