using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Reporting.Application.Exports;

namespace Reporting.Infrastructure.Exports;

/// <summary>
/// El informe como CSV.
///
/// Tres decisiones que parecen detalles y son la diferencia entre un fichero que se abre y uno
/// que no:
///
/// <b>Lleva BOM.</b> Sin él, Excel en Windows abre el fichero como ANSI y cualquier acento sale
/// roto: «Diseño» se convierte en «DiseÃ±o». El BOM es lo que le dice que es UTF-8.
///
/// <b>El separador es el punto y coma.</b> En la configuración regional española la coma es el
/// separador decimal, así que un CSV separado por comas mete «1,5» en dos columnas. Excel,
/// además, respeta el punto y coma en esa configuración.
///
/// <b>No lleva título ni subtítulo.</b> Un CSV es una tabla, y una línea de título antes de los
/// encabezados desplaza todo: quien lo abra con una herramienta verá el título como nombre de la
/// primera columna. El título va en el nombre del fichero, que es donde se busca.
/// </summary>
public sealed class CsvWriter : IReportWriter
{
    public string Format => "Csv";
    public string ContentType => "text/csv; charset=utf-8";
    public string Extension => ".csv";

    private const char Separator = ';';

    public byte[] Write(ReportTable table)
    {
        table.Validate();

        var text = new StringBuilder();
        text.AppendLine(string.Join(Separator, table.Columns.Select(Escape)));

        foreach (var row in table.Rows)
            text.AppendLine(string.Join(Separator, row.Select(Escape)));

        // El BOM se antepone a mano. Es lo que parece un detalle y no lo es: `GetBytes` **nunca**
        // escribe el preámbulo, por mucho que la codificación se construya con
        // `encoderShouldEmitUTF8Identifier: true` —esa opción sólo la mira un `StreamWriter`—.
        // Escrito de la forma evidente, el fichero salía sin BOM y Excel en Windows lo abría como
        // ANSI: «Diseño» se convertía en «DiseÃ±o». Lo cazó la prueba, no la lectura del código.
        var codificacion = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var content = codificacion.GetBytes(text.ToString());

        return [.. Encoding.UTF8.GetPreamble(), .. content];
    }

    /// <summary>
    /// Entrecomilla lo que haga falta, y sólo eso.
    ///
    /// Un valor con separador, comillas o salto de línea va entre comillas, y las comillas de
    /// dentro se duplican, que es lo que dice el RFC 4180. Sin esto, una descripción de tarea
    /// con un punto y coma parte la fila y descuadra el resto del fichero.
    /// </summary>
    private static string Escape(string? value)
    {
        value ??= string.Empty;

        var needs = value.Contains(Separator) || value.Contains('"')
                       || value.Contains('\n') || value.Contains('\r');

        return needs ? '"' + value.Replace("\"", "\"\"") + '"' : value;
    }
}
