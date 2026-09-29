using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Reporting.Application.Exports;

namespace Reporting.Infrastructure.Exports;

/// <summary>
/// El informe como hoja de cálculo.
///
/// El título va en las primeras filas y la tabla debajo con encabezado congelado y autofiltro,
/// que es lo que espera quien abre un informe en Excel: poder ordenar y filtrar sin preparar
/// nada.
/// </summary>
public sealed class ExcelWriter : IReportWriter
{
    public string Format => "Excel";
    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public string Extension => ".xlsx";

    /// <summary>
    /// Cuánto se deja ensanchar una columna al ajustarla al contenido.
    ///
    /// Sin tope, una descripción larga produce una columna de dos metros que deja el resto de la
    /// hoja fuera de la pantalla, y el fichero se abre inservible.
    /// </summary>
    private const double MaxWidth = 60;

    public byte[] Write(ReportTable table)
    {
        table.Validate();

        using var workbook = new XLWorkbook();

        // El nombre de la hoja tiene límites de Excel —31 caracteres y sin ciertos signos— y
        // pasarse hace que el fichero se abra «reparado» o no se abra. Se recorta aquí.
        var sheet = workbook.Worksheets.Add(ValidSheetName(table.Title));

        sheet.Cell(1, 1).Value = table.Title;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;

        var currentRow = 2;

        if (!string.IsNullOrWhiteSpace(table.Subtitle))
        {
            sheet.Cell(currentRow, 1).Value = table.Subtitle;
            sheet.Cell(currentRow, 1).Style.Font.FontColor = XLColor.Gray;
            currentRow++;
        }

        currentRow++; // Una fila en blanco entre la cabecera y la tabla.
        var headerRow = currentRow;

        for (var c = 0; c < table.Columns.Count; c++)
        {
            var cell = sheet.Cell(headerRow, c + 1);
            cell.Value = table.Columns[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF2FF");
        }

        for (var f = 0; f < table.Rows.Count; f++)
        {
            for (var c = 0; c < table.Columns.Count; c++)
            {
                // Se escribe como texto a propósito. La tabla ya trae los valores formateados
                // para el idioma de quien lee, y dejar que Excel adivine el tipo convierte
                // referencias como «1-2» en fechas: es el clásico destrozo silencioso de los CSV
                // y aquí no se repite.
                sheet.Cell(headerRow + 1 + f, c + 1).SetValue(table.Rows[f][c]);
            }
        }

        if (!table.IsEmpty)
        {
            var range = sheet.Range(headerRow, 1, headerRow + table.Rows.Count, table.Columns.Count);
            range.SetAutoFilter();
        }

        sheet.SheetView.FreezeRows(headerRow);
        sheet.Columns().AdjustToContents(1, MaxWidth);

        using var memory = new MemoryStream();
        workbook.SaveAs(memory);
        return memory.ToArray();
    }

    private static string ValidSheetName(string title)
    {
        var clean = new string(title.Where(c => !"[]:*?/\\".Contains(c)).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(clean)) clean = "Informe";
        return clean.Length > 31 ? clean[..31] : clean;
    }
}
