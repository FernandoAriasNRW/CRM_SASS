using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Reporting.Application.Exports;

namespace Reporting.Infrastructure.Exports;

/// <summary>
/// El informe como PDF.
///
/// <b>Se genera con QuestPDF y no con un navegador sin cabeza.</b> La alternativa habitual
/// —renderizar HTML con Chrome— obliga a meter un navegador entero en el contenedor de la API,
/// con sus dependencias del sistema, sus actualizaciones de seguridad y su consumo de memoria.
/// Para una tabla, eso es traer una imprenta para escribir una nota.
///
/// La licencia comunitaria de QuestPDF hay que declararla; se hace en <c>Program.cs</c> al
/// arrancar, no aquí, porque es una decisión de la aplicación y no de este escritor.
/// </summary>
public sealed class PdfWriter : IReportWriter
{
    public string Format => "Pdf";
    public string ContentType => "application/pdf";
    public string Extension => ".pdf";

    /// <summary>
    /// A partir de cuántas columnas se pasa a horizontal.
    ///
    /// Una tabla de ocho columnas en vertical deja el texto en tiras de dos palabras. Es
    /// preferible girar la hoja que entregar algo que no se lee.
    /// </summary>
    private const int ColumnsForLandscape = 5;

    public byte[] Write(ReportTable table)
    {
        table.Validate();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(table.Columns.Count > ColumnsForLandscape
                    ? PageSizes.A4.Landscape()
                    : PageSizes.A4);

                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(header =>
                {
                    header.Item().Text(table.Title).FontSize(16).SemiBold();

                    if (!string.IsNullOrWhiteSpace(table.Subtitle))
                        header.Item().Text(table.Subtitle).FontSize(9).FontColor(Colors.Grey.Darken1);

                    header.Item().PaddingTop(8);
                });

                page.Content().Table(box =>
                {
                    box.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in table.Columns)
                            columns.RelativeColumn();
                    });

                    // El encabezado se declara como tal para que QuestPDF lo repita en cada
                    // página. Sin eso, a partir de la segunda hoja las columnas no tienen nombre
                    // y hay que volver a la primera para saber qué se está mirando.
                    box.Header(heading =>
                    {
                        foreach (var column in table.Columns)
                        {
                            heading.Cell()
                                .Background(Colors.Grey.Lighten3)
                                .Padding(4)
                                .Text(column).SemiBold();
                        }
                    });

                    foreach (var row in table.Rows)
                    {
                        foreach (var cell in row)
                        {
                            box.Cell()
                                .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                .Padding(4)
                                .Text(cell);
                        }
                    }
                });

                // Un informe vacío lo dice. Sin este aviso son dos páginas de encabezados y
                // nada más, y quien lo recibe no sabe si es que no hay datos o si falló algo.
                if (table.IsEmpty)
                {
                    page.Footer().PaddingTop(10)
                        .Text("No hay datos para este informe.")
                        .FontColor(Colors.Grey.Darken1).Italic();
                }
                else
                {
                    page.Footer().AlignRight().Text(text =>
                    {
                        text.CurrentPageNumber();
                        text.Span(" de ");
                        text.TotalPages();
                    });
                }
            });
        });

        return document.GeneratePdf();
    }
}
