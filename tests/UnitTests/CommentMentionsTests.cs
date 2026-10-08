using Comments.Domain.Entities;
using Comments.Domain.Events;
using Comments.Domain.Mentions;
using FluentAssertions;
using Xunit;

namespace UnitTests;

/// <summary>
/// Las menciones dentro de un comentario: cómo se leen del texto y cuándo se avisa de ellas.
///
/// El formato <c>@[Nombre](Tipo:id)</c> es un contrato con la pantalla. Si cambiara de un lado y
/// no del otro, las menciones dejarían de reconocerse sin dar ningún error.
/// </summary>
public sealed class CommentMentionsTests
{
    private static readonly Guid Ana = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Task = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Team = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static string Person(Guid id, string name) => $"@[{name}](Person:{id})";

    private static Comment New(string text)
        => Comment.Create(DateTime.UtcNow, Guid.NewGuid(), CommentableEntityTypes.Task, Guid.NewGuid(), Guid.NewGuid(), text);

    [Fact]
    public void Every_kind_of_mention_is_read_with_its_name()
    {
        var text = $"{Person(Ana, "Ana Pérez")} mira #@[Integrar la pasarela](Task:{Task}) con @[Soporte](Team:{Team})";

        var mentions = CommentMentionReader.Read(text);

        mentions.Select(m => (m.Type, m.EntityId, m.Label)).Should().BeEquivalentTo(new[]
        {
            ("Person", Ana, "Ana Pérez"),
            ("Task", Task, "Integrar la pasarela"),
            ("Team", Team, "Soporte"),
        });
    }

    [Fact]
    public void An_unknown_type_or_a_repeated_mention_does_not_count_twice()
    {
        var text = $"{Person(Ana, "Ana")} y otra vez {Person(Ana, "Ana")} y @[Algo](Planet:{Task})";

        CommentMentionReader.Read(text).Should().ContainSingle(m => m.EntityId == Ana);
    }

    [Fact]
    public void The_plain_text_shows_names_not_identifiers()
    {
        var text = $"Hola {Person(Ana, "Ana")}, revisa @[La pasarela](Task:{Task})";

        CommentMentionReader.ToPlainText(text).Should().Be("Hola @Ana, revisa #La pasarela");
    }

    [Fact]
    public void Creating_a_comment_announces_everyone_it_mentions()
    {
        var comment = New($"{Person(Ana, "Ana")} échale un ojo");

        comment.Mentions.Should().ContainSingle(m => m.EntityId == Ana);
        comment.DomainEvents.OfType<CommentMentionsAddedEvent>().Single()
            .Mentions.Should().Equal(new MentionedEntity("Person", Ana));
    }

    /// <summary>
    /// Corregir una errata no vuelve a avisar a quien ya estaba mencionado: sólo se anuncian las
    /// menciones nuevas.
    /// </summary>
    [Fact]
    public void Editing_announces_only_the_new_mentions()
    {
        var comment = New($"{Person(Ana, "Ana")} échale un ojo");
        comment.ClearDomainEvents();

        comment.Edit(DateTime.UtcNow, comment.AuthorId, $"{Person(Ana, "Ana")} échale un ojo con @[Soporte](Team:{Team})");

        comment.Mentions.Should().HaveCount(2);
        comment.DomainEvents.OfType<CommentMentionsAddedEvent>().Single()
            .Mentions.Should().Equal(new MentionedEntity("Team", Team));
    }

    [Fact]
    public void A_comment_without_mentions_announces_none()
    {
        var comment = New("Nada que mencionar");

        comment.Mentions.Should().BeEmpty();
        comment.DomainEvents.OfType<CommentMentionsAddedEvent>().Should().BeEmpty();
    }
}
