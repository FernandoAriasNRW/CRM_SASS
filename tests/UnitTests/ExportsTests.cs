using System.Text;
using FluentAssertions;
using Reporting.Application.Exports;
using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;
using Reporting.Infrastructure.Exports;
using Xunit;

namespace UnitTests;

/// <summary>
/// El ciclo de vida de una exportación.
///
/// Lo que se vigila aquí no es que los estados cambien —eso es evidente leyendo la clase— sino
/// las dos cosas que el plan señalaba como las que pudren estos sistemas: que nada se quede en
/// «generando» para siempre, y que un fallo diga por qué.
/// </summary>
public class ExportTests
{
    private static Export New()
        => Export.Request(DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ReportFormat.Csv).Value!;

    [Fact]
    public void Starts_pending()
    {
        var export = New();

        export.Status.Should().Be(ExportStatus.Pending);
        export.Attempts.Should().Be(0);
        export.FileName.Should().BeNull();
    }

    [Fact]
    public void It_cannot_be_requested_without_a_requester()
    {
        var result = Export.Request(DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, ReportFormat.Pdf);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Export.Rules.MissingRequester);
    }

    /// <summary>Dos trabajadores compitiendo: el segundo se va con las manos vacías, sin error.</summary>
    [Fact]
    public void Only_one_worker_takes_it()
    {
        var export = New();

        export.Start(DateTime.UtcNow).Should().BeTrue();
        export.Start(DateTime.UtcNow).Should().BeFalse("otro ya la cogió; no es un error, es una carrera perdida");

        export.Attempts.Should().Be(1, "el que pierde no cuenta como intento");
    }

    [Fact]
    public void Finishing_leaves_it_ready_with_its_file()
    {
        var export = New();
        export.Start(DateTime.UtcNow);

        export.Finish(DateTime.UtcNow, "informe.csv", 1234);

        export.Status.Should().Be(ExportStatus.Ready);
        export.FileName.Should().Be("informe.csv");
        export.SizeBytes.Should().Be(1234);
        export.Error.Should().BeNull();
    }

    [Fact]
    public void Failing_keeps_the_reason()
    {
        var export = New();
        export.Start(DateTime.UtcNow);

        export.Fail(DateTime.UtcNow, "La base de datos no responde");

        export.Status.Should().Be(ExportStatus.Failed);
        export.Error.Should().Be("La base de datos no responde");
    }

    /// <summary>
    /// Un fallo siempre deja explicación, aunque quien lo provoque no dé ninguna.
    ///
    /// Una excepción con mensaje vacío existe —las hay— y sin esto la pantalla enseñaría
    /// «Fallida:» y nada más, que es lo mismo que no decir nada.
    /// </summary>
    [Fact]
    public void A_failure_without_message_still_explains_something()
    {
        var export = New();
        export.Start(DateTime.UtcNow);

        export.Fail(DateTime.UtcNow, "   ");

        export.Error.Should().Be(Export.Rules.FailureWithoutReason);
    }

    [Fact]
    public void Finishing_raises_the_event_that_triggers_the_notification()
    {
        var export = New();
        export.Start(DateTime.UtcNow);

        export.Finish(DateTime.UtcNow, "informe.pdf", 10);

        export.DomainEvents.Should().ContainSingle(e => e is Reporting.Domain.Events.ExportReadyEvent);
    }

    [Fact]
    public void Failing_also_raises_an_event_because_failures_are_notified_too()
    {
        var export = New();
        export.Start(DateTime.UtcNow);

        export.Fail(DateTime.UtcNow, "se rompió");

        export.DomainEvents.Should().ContainSingle(e => e is Reporting.Domain.Events.ExportFailedEvent);
    }

    [Fact]
    public void A_just_started_one_is_not_taken_again()
    {
        var export = New();
        export.Start(DateTime.UtcNow);

        export.CanStart(DateTime.UtcNow).Should().BeFalse(
            "acaba de empezar; recogerla ahora sería generarla dos veces a la vez");
    }

    [Fact]
    public void A_ready_one_is_not_generated_again()
    {
        var export = New();
        export.Start(DateTime.UtcNow);
        export.Finish(DateTime.UtcNow, "x.csv", 1);

        export.CanStart(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void Requeue_puts_it_back_in_the_queue()
    {
        var export = New();
        export.Start(DateTime.UtcNow);
        export.Fail(DateTime.UtcNow, "un fallo pasajero");

        export.Requeue();

        export.Status.Should().Be(ExportStatus.Pending);
        export.CanStart(DateTime.UtcNow).Should().BeTrue();
    }

    /// <summary>
    /// Se agotan los intentos y deja de reintentarse.
    ///
    /// Sin este tope, una exportación que falla siempre —un informe cuyo tipo no sabe generar
    /// datos, por ejemplo— se reintentaría cada cinco segundos para siempre, llenando el registro
    /// y consumiendo el trabajador que otros necesitan.
    /// </summary>
    [Fact]
    public void Attempts_run_out()
    {
        var export = New();

        for (var i = 0; i < Export.MaxAttempts; i++)
        {
            export.Requeue();
            export.Start(DateTime.UtcNow).Should().BeTrue();
        }

        export.Attempts.Should().Be(Export.MaxAttempts);
        export.HasAttemptsLeft().Should().BeFalse();
    }
}

/// <summary>
/// Los tres escritores de fichero.
///
/// Son código puro —tabla entra, bytes salen— así que se prueban sin base de datos. Lo que se
/// comprueba es lo que rompe un fichero en manos de quien lo abre: el escapado, el BOM y la
/// coherencia entre columnas y filas.
/// </summary>
public class ReportWriterTests
{
    private static ReportTable Table(params string[][] rows)
        => new("Informe de prueba", "Un subtítulo",
               ["Nombre", "Cantidad"],
               rows.Select(f => (IReadOnlyList<string>)f).ToList());

    #region CSV

    /// <summary>
    /// El BOM, que es lo que separa un CSV legible de uno con los acentos rotos.
    ///
    /// Sin él, Excel en Windows abre el fichero como ANSI y «Diseño» sale como «DiseÃ±o». Es el
    /// primer motivo por el que alguien dice que «la exportación no funciona».
    /// </summary>
    [Fact]
    public void Csv_has_a_bom_so_accents_survive()
    {
        var bytes = new CsvWriter().Write(Table(["Diseño", "3"]));

        bytes.Take(3).Should().Equal((byte)0xEF, (byte)0xBB, (byte)0xBF);
        Encoding.UTF8.GetString(bytes).Should().Contain("Diseño");
    }

    /// <summary>
    /// El separador es el punto y coma porque la coma es el separador decimal en español.
    ///
    /// Con comas, «1,5» acabaría partido en dos columnas.
    /// </summary>
    [Fact]
    public void Csv_is_semicolon_separated()
    {
        var bytes = new CsvWriter().Write(Table(["Tarea", "1,5"]));
        var text = Encoding.UTF8.GetString(bytes);

        text.Should().Contain("Nombre;Cantidad");

        // «1,5» va **sin comillas**, y eso es exactamente lo que se busca: con la coma como
        // separador habría que entrecomillar cada número decimal del informe, y cualquier
        // herramienta que se saltara el entrecomillado partiría la fila. Con punto y coma, un
        // decimal español es un valor normal.
        text.Should().Contain("Tarea;1,5");
    }

    /// <summary>
    /// Un valor con el separador dentro se entrecomilla, o parte la fila.
    ///
    /// Es el fallo clásico: una descripción con un punto y coma descuadra el fichero **a partir
    /// de ahí**, así que el síntoma aparece en filas que no tienen nada que ver.
    /// </summary>
    [Fact]
    public void Csv_quotes_what_would_split_the_row()
    {
        var bytes = new CsvWriter().Write(Table(["Revisar; luego cerrar", "2"]));
        var text = Encoding.UTF8.GetString(bytes);

        text.Should().Contain("\"Revisar; luego cerrar\";2");
    }

    [Fact]
    public void Csv_doubles_inner_quotes()
    {
        var bytes = new CsvWriter().Write(Table(["Dijo \"vale\"", "1"]));

        Encoding.UTF8.GetString(bytes).Should().Contain("\"Dijo \"\"vale\"\"\"");
    }

    [Fact]
    public void Csv_quotes_line_breaks()
    {
        var bytes = new CsvWriter().Write(Table(["Primera\nSegunda", "1"]));

        Encoding.UTF8.GetString(bytes).Should().Contain("\"Primera\nSegunda\"");
    }

    /// <summary>
    /// El CSV no lleva título.
    ///
    /// Una línea de título antes de los encabezados desplaza toda la tabla: cualquier herramienta
    /// que lo abra tomará el título como el nombre de la primera columna.
    /// </summary>
    [Fact]
    public void Csv_starts_with_headers_not_the_title()
    {
        var bytes = new CsvWriter().Write(Table(["Algo", "1"]));
        var first = Encoding.UTF8.GetString(bytes).Split('\n')[0].TrimStart('﻿');

        first.Should().StartWith("Nombre;Cantidad");
    }

    #endregion

    #region Coherencia

    /// <summary>
    /// Una fila con menos celdas que columnas falla **antes** de escribir nada, y con nombre.
    ///
    /// Se comprueba en la tabla y no dentro de cada escritor porque los tres fallarían distinto:
    /// el CSV escribiría una fila corta que desalinea el fichero de ahí en adelante, ClosedXML
    /// lanzaría y el PDF pintaría una celda vacía. Tres síntomas para una sola causa.
    /// </summary>
    [Fact]
    public void A_misaligned_row_is_detected_before_writing()
    {
        var table = new ReportTable("X", null, ["A", "B"], [["solo una"]]);

        var act = () => new CsvWriter().Write(table);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*1 celdas*2 columnas*");
    }

    [Fact]
    public void A_table_without_columns_is_not_written()
    {
        var table = new ReportTable("X", null, [], []);

        var act = () => new CsvWriter().Write(table);

        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Excel y PDF

    /// <summary>
    /// El Excel sale y es un fichero de verdad.
    ///
    /// Se comprueba la firma del ZIP —un `.xlsx` es un zip— en lugar de abrirlo con la propia
    /// librería: comprobar un fichero con la misma librería que lo escribió no descubre gran
    /// cosa.
    /// </summary>
    [Fact]
    public void Excel_is_a_valid_xlsx()
    {
        var bytes = new ExcelWriter().Write(Table(["Algo", "1"], ["Otra cosa", "2"]));

        bytes.Length.Should().BeGreaterThan(0);
        // Un .xlsx es un zip: empieza por «PK».
        bytes.Take(2).Should().Equal((byte)0x50, (byte)0x4B);
    }

    [Fact]
    public void Pdf_is_a_valid_pdf()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var bytes = new PdfWriter().Write(Table(["Algo", "1"]));

        Encoding.ASCII.GetString(bytes.Take(5).ToArray()).Should().Be("%PDF-");
    }

    /// <summary>
    /// Un informe sin datos genera fichero igual, y no un error.
    ///
    /// «No hay datos» es una respuesta legítima de un informe. Fallar aquí obligaría a quien lo
    /// pidió a preguntarse si se rompió algo.
    /// </summary>
    [Fact]
    public void An_empty_report_still_produces_a_file()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var empty = new ReportTable("Sin datos", null, ["A"], []);

        new CsvWriter().Write(empty).Length.Should().BeGreaterThan(0);
        new ExcelWriter().Write(empty).Length.Should().BeGreaterThan(0);
        new PdfWriter().Write(empty).Length.Should().BeGreaterThan(0);
    }

    #endregion

    #region El selector

    [Fact]
    public void The_selector_returns_each_format_writer()
    {
        var selector = new ReportWriters([new CsvWriter(), new ExcelWriter(), new PdfWriter()]);

        selector.For("Csv").Extension.Should().Be(".csv");
        selector.For("Excel").Extension.Should().Be(".xlsx");
        selector.For("Pdf").Extension.Should().Be(".pdf");
    }

    /// <summary>
    /// Un formato sin escritor falla diciendo cuáles hay.
    ///
    /// Devolver nulo convertiría esto, tres capas más allá, en «la exportación falló» sin
    /// explicación: justo lo que la pantalla de informes ya hizo una vez con los tipos.
    /// </summary>
    [Fact]
    public void A_format_without_writer_says_so_and_lists_the_available_ones()
    {
        var selector = new ReportWriters([new CsvWriter()]);

        var act = () => selector.For("Word");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Word*Csv*");
    }

    #endregion
}
