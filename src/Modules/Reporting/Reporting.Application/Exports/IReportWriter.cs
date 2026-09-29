namespace Reporting.Application.Exports;

/// <summary>
/// Escribe una <see cref="ReportTable"/> en un formato concreto.
///
/// Uno por formato, elegidos por <c>ReportFormat</c>. Devuelve bytes y no un <c>Stream</c>: el
/// resultado se guarda entero de todas formas, y un stream obligaría a cada llamante a acordarse
/// de cerrarlo.
/// </summary>
public interface IReportWriter
{
    /// <summary>El formato que sabe escribir, por nombre de <c>ReportFormat</c>.</summary>
    string Format { get; }

    /// <summary>Lo que va en la cabecera HTTP al descargar.</summary>
    string ContentType { get; }

    /// <summary>La extensión, con punto.</summary>
    string Extension { get; }

    byte[] Write(ReportTable table);
}
