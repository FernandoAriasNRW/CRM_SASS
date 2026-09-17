using FluentAssertions;
using Docs.Domain.Menciones;
using Xunit;

namespace UnitTests;

/// <summary>
/// El lector de menciones.
///
/// <b>Aquí vive el contrato con el editor.</b> La extensión de menciones tiene que escribir
/// <c>data-mencion-tipo</c> y <c>data-mencion-id</c> en cada mención; si dejara de hacerlo, esto
/// devolvería cero y las menciones desaparecerían <b>sin dar ningún error</b>. Estas pruebas fijan
/// el formato por el lado del servidor, y una de integración comprueba la unión entera.
///
/// Lo que se prueba con más cuidado es lo que rompe una expresión regular sobre HTML: el orden de
/// los atributos, el tipo de comillas y los atributos con nombres parecidos.
/// </summary>
public class LectorDeMencionesTests
{
    private static readonly Guid Tarea = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Ticket = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Encuentra_una_mencion_normal()
    {
        var html = $"""<p>Hablamos de <span data-mencion-tipo="Tarea" data-mencion-id="{Tarea}">Migrar la API</span> ayer.</p>""";

        var menciones = LectorDeMenciones.Leer(html);

        var mencion = menciones.Should().ContainSingle().Subject;
        mencion.Tipo.Should().Be("Tarea");
        mencion.EntidadId.Should().Be(Tarea);
        mencion.VisibleText.Should().Be("Migrar la API");
    }

    /// <summary>
    /// El orden de los atributos no importa.
    ///
    /// El editor los escribe en el orden que le apetece, y puede cambiarlo entre versiones. Una
    /// expresión regular que exija un orden concreto funciona hasta el día que actualicen TipTap.
    /// </summary>
    [Fact]
    public void El_orden_de_los_atributos_da_igual()
    {
        var html = $"""<span data-mencion-id="{Tarea}" class="mencion" data-mencion-tipo="Tarea">Algo</span>""";

        LectorDeMenciones.Leer(html).Should().ContainSingle(m => m.EntidadId == Tarea);
    }

    /// <summary>Comillas simples: las escriben algunos serializadores y son HTML válido.</summary>
    [Fact]
    public void Las_comillas_simples_valen()
    {
        var html = $"<span data-mencion-tipo='Ticket' data-mencion-id='{Ticket}'>Un ticket</span>";

        LectorDeMenciones.Leer(html).Should().ContainSingle(m => m.Tipo == "Ticket");
    }

    [Fact]
    public void Varias_menciones_distintas_se_encuentran_todas()
    {
        var html = $"""
            <p><span data-mencion-tipo="Tarea" data-mencion-id="{Tarea}">Una</span> y
            <span data-mencion-tipo="Ticket" data-mencion-id="{Ticket}">Otro</span></p>
            """;

        LectorDeMenciones.Leer(html).Should().HaveCount(2);
    }

    /// <summary>
    /// La misma tarea mencionada tres veces es **una** mención.
    ///
    /// La pregunta que contesta esto es «¿qué documentos hablan de esta tarea?», no «cuántas
    /// veces». Sin agrupar, la lista de «mencionado en» repetiría el mismo documento.
    /// </summary>
    [Fact]
    public void La_misma_entidad_mencionada_varias_veces_cuenta_una()
    {
        var html = $"""
            <p><span data-mencion-tipo="Tarea" data-mencion-id="{Tarea}">Una</span></p>
            <p><span data-mencion-tipo="Tarea" data-mencion-id="{Tarea}">Una otra vez</span></p>
            """;

        LectorDeMenciones.Leer(html).Should().ContainSingle();
    }

    #region Lo que se descarta

    /// <summary>
    /// Una mención sin identificador se descarta.
    ///
    /// No se puede enlazar, y guardarla dejaría una entrada muerta en «mencionado en» que nadie
    /// sabría de dónde salió.
    /// </summary>
    [Fact]
    public void Una_mencion_sin_identificador_se_descarta()
    {
        LectorDeMenciones.Leer("""<span data-mencion-tipo="Tarea">Sin id</span>""").Should().BeEmpty();
    }

    [Fact]
    public void Un_identificador_que_no_es_un_guid_se_descarta()
    {
        LectorDeMenciones.Leer("""<span data-mencion-tipo="Tarea" data-mencion-id="pepito">X</span>""")
            .Should().BeEmpty();
    }

    /// <summary>Un tipo que no se puede mencionar se descarta: la lista es cerrada a propósito.</summary>
    [Fact]
    public void Un_tipo_desconocido_se_descarta()
    {
        LectorDeMenciones.Leer($"""<span data-mencion-tipo="Factura" data-mencion-id="{Tarea}">X</span>""")
            .Should().BeEmpty();
    }

    [Fact]
    public void El_guid_vacio_se_descarta()
    {
        LectorDeMenciones.Leer($"""<span data-mencion-tipo="Tarea" data-mencion-id="{Guid.Empty}">X</span>""")
            .Should().BeEmpty();
    }

    [Fact]
    public void Un_documento_sin_menciones_no_devuelve_ninguna()
    {
        LectorDeMenciones.Leer("<p>Un párrafo normal con <strong>negrita</strong>.</p>").Should().BeEmpty();
        LectorDeMenciones.Leer("").Should().BeEmpty();
        LectorDeMenciones.Leer(null).Should().BeEmpty();
    }

    /// <summary>
    /// Un atributo con nombre parecido no cuela.
    ///
    /// Es lo que hace fallar a las expresiones regulares sobre HTML: `data-mencion-tipografia`
    /// contiene `data-mencion-tipo` como prefijo. Se comprueba que el patrón exige el signo igual.
    /// </summary>
    [Fact]
    public void Un_atributo_con_nombre_parecido_no_se_confunde()
    {
        var html = $"""<span data-mencion-tipografia="Tarea" data-mencion-id="{Tarea}">X</span>""";

        LectorDeMenciones.Leer(html).Should().BeEmpty();
    }

    #endregion

    /// <summary>El texto visible se limpia de etiquetas y de entidades HTML.</summary>
    [Fact]
    public void El_texto_visible_llega_limpio()
    {
        var html = $"""<span data-mencion-tipo="Tarea" data-mencion-id="{Tarea}">Dise&#241;o &amp; UX</span>""";

        LectorDeMenciones.Leer(html).Single().VisibleText.Should().Be("Diseño & UX");
    }

    /// <summary>
    /// Un pegado gigantesco no llena la tabla.
    ///
    /// Un documento con cien tareas mencionadas es legítimo; uno con diez mil es un accidente, y
    /// sin tope llenaría la lista de «mencionado en» de cada una.
    /// </summary>
    [Fact]
    public void Hay_un_tope_de_menciones_por_pagina()
    {
        var muchas = string.Join("", Enumerable.Range(0, LectorDeMenciones.MaximoPorPagina + 50)
            .Select(_ => $"""<span data-mencion-tipo="Tarea" data-mencion-id="{Guid.NewGuid()}">X</span>"""));

        LectorDeMenciones.Leer(muchas).Should().HaveCount(LectorDeMenciones.MaximoPorPagina);
    }

    [Fact]
    public void El_texto_visible_muy_largo_se_recorta_al_guardar()
    {
        var larguisimo = new string('a', 500);

        var mencion = MencionEnDocumento.Crear(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Tarea", Tarea, larguisimo);

        mencion.VisibleText.Length.Should().Be(200);
    }
}
