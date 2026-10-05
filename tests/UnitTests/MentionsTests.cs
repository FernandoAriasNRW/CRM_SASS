using FluentAssertions;
using Docs.Domain.Mentions;
using Xunit;

namespace UnitTests;

/// <summary>
/// El lector de menciones.
///
/// <b>Aquí vive el contrato con el editor.</b> La extensión de menciones tiene que escribir
/// <c>data-mention-type</c> y <c>data-mention-id</c> en cada mención; si dejara de hacerlo, esto
/// devolvería cero y las menciones desaparecerían <b>sin dar ningún error</b>. Estas pruebas fijan
/// el formato por el lado del servidor, y una de integración comprueba la unión entera.
///
/// Lo que se prueba con más cuidado es lo que rompe una expresión regular sobre HTML: el orden de
/// los atributos, el tipo de comillas y los atributos con nombres parecidos.
/// </summary>
public class MentionReaderTests
{
    private static readonly Guid TaskMention = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Ticket = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Finds_a_plain_mention()
    {
        var html = $"""<p>Hablamos de <span data-mention-type="Task" data-mention-id="{TaskMention}">Migrar la API</span> ayer.</p>""";

        var mentions = MentionReader.Read(html);

        var mention = mentions.Should().ContainSingle().Subject;
        mention.Type.Should().Be("Task");
        mention.EntityId.Should().Be(TaskMention);
        mention.VisibleText.Should().Be("Migrar la API");
    }

    /// <summary>
    /// El orden de los atributos no importa.
    ///
    /// El editor los escribe en el orden que le apetece, y puede cambiarlo entre versiones. Una
    /// expresión regular que exija un orden concreto funciona hasta el día que actualicen TipTap.
    /// </summary>
    [Fact]
    public void Attribute_order_does_not_matter()
    {
        var html = $"""<span data-mention-id="{TaskMention}" class="mention" data-mention-type="Task">Algo</span>""";

        MentionReader.Read(html).Should().ContainSingle(m => m.EntityId == TaskMention);
    }

    /// <summary>Comillas simples: las escriben algunos serializadores y son HTML válido.</summary>
    [Fact]
    public void Single_quotes_work()
    {
        var html = $"<span data-mention-type='Ticket' data-mention-id='{Ticket}'>Un ticket</span>";

        MentionReader.Read(html).Should().ContainSingle(m => m.Type == "Ticket");
    }

    [Fact]
    public void Several_distinct_mentions_are_all_found()
    {
        var html = $"""
            <p><span data-mention-type="Task" data-mention-id="{TaskMention}">Una</span> y
            <span data-mention-type="Ticket" data-mention-id="{Ticket}">Otro</span></p>
            """;

        MentionReader.Read(html).Should().HaveCount(2);
    }

    /// <summary>
    /// La misma tarea mencionada tres veces es **una** mención.
    ///
    /// La pregunta que contesta esto es «¿qué documentos hablan de esta tarea?», no «cuántas
    /// veces». Sin agrupar, la lista de «mencionado en» repetiría el mismo documento.
    /// </summary>
    [Fact]
    public void The_same_entity_mentioned_several_times_counts_once()
    {
        var html = $"""
            <p><span data-mention-type="Task" data-mention-id="{TaskMention}">Una</span></p>
            <p><span data-mention-type="Task" data-mention-id="{TaskMention}">Una otra vez</span></p>
            """;

        MentionReader.Read(html).Should().ContainSingle();
    }

    #region Lo que se descarta

    /// <summary>
    /// Una mención sin identificador se descarta.
    ///
    /// No se puede enlazar, y guardarla dejaría una entrada muerta en «mencionado en» que nadie
    /// sabría de dónde salió.
    /// </summary>
    [Fact]
    public void A_mention_without_identifier_is_discarded()
    {
        MentionReader.Read("""<span data-mention-type="Task">Sin id</span>""").Should().BeEmpty();
    }

    [Fact]
    public void An_identifier_that_is_not_a_guid_is_discarded()
    {
        MentionReader.Read("""<span data-mention-type="Task" data-mention-id="pepito">X</span>""")
            .Should().BeEmpty();
    }

    /// <summary>Un tipo que no se puede mencionar se descarta: la lista es cerrada a propósito.</summary>
    [Fact]
    public void An_unknown_type_is_discarded()
    {
        MentionReader.Read($"""<span data-mention-type="Factura" data-mention-id="{TaskMention}">X</span>""")
            .Should().BeEmpty();
    }

    [Fact]
    public void An_empty_guid_is_discarded()
    {
        MentionReader.Read($"""<span data-mention-type="Task" data-mention-id="{Guid.Empty}">X</span>""")
            .Should().BeEmpty();
    }

    [Fact]
    public void A_document_without_mentions_returns_none()
    {
        MentionReader.Read("<p>Un párrafo normal con <strong>negrita</strong>.</p>").Should().BeEmpty();
        MentionReader.Read("").Should().BeEmpty();
        MentionReader.Read(null).Should().BeEmpty();
    }

    /// <summary>
    /// Un atributo con nombre parecido no cuela.
    ///
    /// Es lo que hace fallar a las expresiones regulares sobre HTML: `data-mention-typeface`
    /// contiene `data-mention-type` como prefijo. Se comprueba que el patrón exige el signo igual.
    /// </summary>
    [Fact]
    public void A_similarly_named_attribute_is_not_confused()
    {
        var html = $"""<span data-mention-typeface="Task" data-mention-id="{TaskMention}">X</span>""";

        MentionReader.Read(html).Should().BeEmpty();
    }

    #endregion

    /// <summary>El texto visible se limpia de etiquetas y de entidades HTML.</summary>
    [Fact]
    public void The_visible_text_arrives_clean()
    {
        var html = $"""<span data-mention-type="Task" data-mention-id="{TaskMention}">Dise&#241;o &amp; UX</span>""";

        MentionReader.Read(html).Single().VisibleText.Should().Be("Diseño & UX");
    }

    /// <summary>
    /// Un pegado gigantesco no llena la tabla.
    ///
    /// Un documento con cien tareas mencionadas es legítimo; uno con diez mil es un accidente, y
    /// sin tope llenaría la lista de «mencionado en» de cada una.
    /// </summary>
    [Fact]
    public void There_is_a_mention_cap_per_page()
    {
        var many = string.Join("", Enumerable.Range(0, MentionReader.MaxPerPage + 50)
            .Select(_ => $"""<span data-mention-type="Task" data-mention-id="{Guid.NewGuid()}">X</span>"""));

        MentionReader.Read(many).Should().HaveCount(MentionReader.MaxPerPage);
    }

    [Fact]
    public void A_very_long_visible_text_is_trimmed_when_saved()
    {
        var larguisimo = new string('a', 500);

        var mention = DocumentMention.Create(
            DateTime.UtcNow,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Task", TaskMention, larguisimo);

        mention.VisibleText.Length.Should().Be(200);
    }
}
