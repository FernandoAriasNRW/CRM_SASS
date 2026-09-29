using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Reporting.Application.Exports;

namespace Reporting.Infrastructure.Exports;

/// <summary>
/// Elige el escritor según el formato pedido.
///
/// Falla con nombre si llega un formato sin escritor, en vez de devolver nulo: un nulo aquí se
/// convertiría, tres capas más allá, en «la exportación falló» sin más explicación.
/// </summary>
public sealed class ReportWriters(IEnumerable<IReportWriter> writers)
{
    public IReportWriter For(string format)
        => writers.FirstOrDefault(e => string.Equals(e.Format, format, StringComparison.OrdinalIgnoreCase))
           ?? throw new InvalidOperationException(
               $"No hay escritor para el formato «{format}». Los que hay: "
               + string.Join(", ", writers.Select(e => e.Format)));
}
