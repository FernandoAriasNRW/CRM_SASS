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
    private static readonly Guid Autor = Guid.NewGuid();
    private static readonly Guid Otro = Guid.NewGuid();

    private static Comment Nuevo(string texto = "Un comentario", Guid? autor = null, Guid? respondeA = null)
        => Comment.Create(
            Guid.NewGuid(), CommentableEntityTypes.Task, Guid.NewGuid(),
            autor ?? Autor, texto, respondeA);

    [Fact]
    public void Un_comentario_guarda_quien_y_cuando_y_emite_evento()
    {
        var antes = DateTime.UtcNow.AddSeconds(-1);

        var comentario = Nuevo();

        comentario.AuthorId.Should().Be(Autor);
        comentario.CreatedAtUtc.Should().BeAfter(antes);
        comentario.EditedAtUtc.Should().BeNull();
        comentario.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<CommentAddedEvent>();
    }

    [Fact]
    public void Un_comentario_vacio_se_rechaza()
    {
        var accion = () => Nuevo("   ");

        accion.Should().Throw<InvalidOperationException>()
            .WithMessage(Comment.Rules.TextRequired);
    }

    [Fact]
    public void Un_comentario_demasiado_largo_se_rechaza()
    {
        var accion = () => Nuevo(new string('x', Comment.MaxLength + 1));

        accion.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void El_texto_se_guarda_sin_espacios_sobrantes()
    {
        Nuevo("  con espacios  ").Text.Should().Be("con espacios");
    }

    [Fact]
    public void Solo_se_puede_comentar_sobre_lo_que_alguien_pinta()
    {
        var accion = () => Comment.Create(
            Guid.NewGuid(), "Factura", Guid.NewGuid(), Autor, "Hola");

        accion.Should().Throw<InvalidOperationException>()
            .WithMessage(Comment.Rules.UnknownEntity);
    }

    /// <summary>Un comentario es de quien lo firma. Ni el administrador puede reescribirlo.</summary>
    [Fact]
    public void Solo_el_autor_puede_editar()
    {
        var comentario = Nuevo();

        var accion = () => comentario.Edit(Otro, "Otra cosa");

        accion.Should().Throw<InvalidOperationException>()
            .WithMessage(Comment.Rules.OnlyAuthorEdits);
        comentario.Text.Should().Be("Un comentario");
    }

    /// <summary>
    /// Un comentario que cambia sin decir que cambió convierte un hilo en algo que no se puede
    /// leer con confianza.
    /// </summary>
    [Fact]
    public void Editar_deja_constancia_de_que_se_edito()
    {
        var comentario = Nuevo();

        comentario.Edit(Autor, "Corregido");

        comentario.Text.Should().Be("Corregido");
        comentario.EditedAtUtc.Should().NotBeNull();
        comentario.DomainEvents.Should().Contain(e => e is CommentEditedEvent);
    }

    [Fact]
    public void Editar_con_texto_vacio_se_rechaza_y_no_toca_el_original()
    {
        var comentario = Nuevo();

        var accion = () => comentario.Edit(Autor, "  ");

        accion.Should().Throw<InvalidOperationException>();
        comentario.Text.Should().Be("Un comentario");
        comentario.EditedAtUtc.Should().BeNull();
    }

    /// <summary>
    /// Borrar sí lo puede hacer quien administra: moderar es parte de su trabajo, y borrar no
    /// pone palabras en boca de nadie.
    /// </summary>
    [Fact]
    public void Lo_borra_su_autor_o_quien_administra()
    {
        var comentario = Nuevo();

        comentario.CanDelete(Autor, "Member").Should().BeTrue();
        comentario.CanDelete(Otro, "Admin").Should().BeTrue();
        comentario.CanDelete(Otro, "Member").Should().BeFalse();
    }

    [Fact]
    public void Una_respuesta_recuerda_a_quien_responde()
    {
        var padre = Guid.NewGuid();

        Nuevo(respondeA: padre).ReplyToId.Should().Be(padre);
    }

    [Fact]
    public void Un_comentario_sin_autor_o_sin_entidad_se_rechaza()
    {
        var sinAutor = () => Comment.Create(
            Guid.NewGuid(), CommentableEntityTypes.Ticket, Guid.NewGuid(), Guid.Empty, "Hola");
        sinAutor.Should().Throw<InvalidOperationException>()
            .WithMessage(Comment.Rules.MissingAuthor);

        var sinEntidad = () => Comment.Create(
            Guid.NewGuid(), CommentableEntityTypes.Ticket, Guid.Empty, Autor, "Hola");
        sinEntidad.Should().Throw<InvalidOperationException>()
            .WithMessage(Comment.Rules.MissingEntity);
    }
}
