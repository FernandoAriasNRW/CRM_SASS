using Comments.Domain.Entities;
using Comments.Domain.Events;
using FluentAssertions;
using Xunit;

namespace UnitTests;

/// <summary>
/// Invariantes del comentario.
///
/// Lo que más importa aquí no es el formato del texto: es **de quién es un comentario**. Si otro
/// lo puede reescribir, la firma deja de significar nada y un hilo deja de poder leerse con
/// confianza.
/// </summary>
public sealed class CommentTests
{
    private static readonly Guid Author = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();

    private static Comment New(string text = "Un comentario", Guid? author = null, Guid? repliesTo = null)
        => Comment.Create(
            DateTime.UtcNow,
            Guid.NewGuid(), CommentableEntityTypes.Task, Guid.NewGuid(),
            author ?? Author, text, repliesTo);

    [Fact]
    public void A_comment_records_who_and_when_and_raises_an_event()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);

        var comment = New();

        comment.AuthorId.Should().Be(Author);
        comment.CreatedAtUtc.Should().BeAfter(before);
        comment.EditedAtUtc.Should().BeNull();
        comment.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<CommentAddedEvent>();
    }

    [Fact]
    public void An_empty_comment_is_rejected()
    {
        var act = () => New("   ");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage(Comment.Rules.TextRequired);
    }

    [Fact]
    public void A_too_long_comment_is_rejected()
    {
        var act = () => New(new string('x', Comment.MaxLength + 1));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void The_text_is_saved_without_extra_whitespace()
    {
        New("  con espacios  ").Text.Should().Be("con espacios");
    }

    [Fact]
    public void Only_entities_someone_renders_can_be_commented()
    {
        var act = () => Comment.Create(
            DateTime.UtcNow,
            Guid.NewGuid(), "Factura", Guid.NewGuid(), Author, "Hola");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage(Comment.Rules.UnknownEntity);
    }

    /// <summary>Un comentario es de quien lo firma. Ni el administrador puede reescribirlo.</summary>
    [Fact]
    public void Only_the_author_can_edit()
    {
        var comment = New();

        var act = () => comment.Edit(DateTime.UtcNow, Other, "Otra cosa");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage(Comment.Rules.OnlyAuthorEdits);
        comment.Text.Should().Be("Un comentario");
    }

    /// <summary>
    /// Un comentario que cambia sin decir que cambió convierte un hilo en algo que no se puede
    /// leer con confianza.
    /// </summary>
    [Fact]
    public void Editing_records_that_it_was_edited()
    {
        var comment = New();

        comment.Edit(DateTime.UtcNow, Author, "Corregido");

        comment.Text.Should().Be("Corregido");
        comment.EditedAtUtc.Should().NotBeNull();
        comment.DomainEvents.Should().Contain(e => e is CommentEditedEvent);
    }

    [Fact]
    public void Editing_with_empty_text_is_rejected_and_keeps_the_original()
    {
        var comment = New();

        var act = () => comment.Edit(DateTime.UtcNow, Author, "  ");

        act.Should().Throw<InvalidOperationException>();
        comment.Text.Should().Be("Un comentario");
        comment.EditedAtUtc.Should().BeNull();
    }

    /// <summary>
    /// Borrar sí lo puede hacer quien administra: moderar es parte de su trabajo, y borrar no
    /// pone palabras en boca de nadie.
    /// </summary>
    [Fact]
    public void Its_author_or_an_admin_can_delete_it()
    {
        var comment = New();

        comment.CanDelete(Author, "Member").Should().BeTrue();
        comment.CanDelete(Other, "Admin").Should().BeTrue();
        comment.CanDelete(Other, "Member").Should().BeFalse();
    }

    [Fact]
    public void A_reply_remembers_what_it_replies_to()
    {
        var parent = Guid.NewGuid();

        New(repliesTo: parent).ReplyToId.Should().Be(parent);
    }

    [Fact]
    public void A_comment_without_author_or_entity_is_rejected()
    {
        var withoutAuthor = () => Comment.Create(
            DateTime.UtcNow,
            Guid.NewGuid(), CommentableEntityTypes.Ticket, Guid.NewGuid(), Guid.Empty, "Hola");
        withoutAuthor.Should().Throw<InvalidOperationException>()
            .WithMessage(Comment.Rules.MissingAuthor);

        var withoutEntity = () => Comment.Create(
            DateTime.UtcNow,
            Guid.NewGuid(), CommentableEntityTypes.Ticket, Guid.Empty, Author, "Hola");
        withoutEntity.Should().Throw<InvalidOperationException>()
            .WithMessage(Comment.Rules.MissingEntity);
    }
}
