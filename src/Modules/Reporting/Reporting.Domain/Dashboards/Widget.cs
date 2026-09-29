using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Domain;

namespace Reporting.Domain.Dashboards;

/// <summary>
/// Un recuadro del panel: <b>un informe, pintado de una forma, en un sitio de la rejilla</b>.
///
/// <b>El widget no sabe consultar nada.</b> Apunta a un informe y ya está. Es la decisión que el
/// plan dejó escrita —«los widgets vienen del reporte, no al revés»— y evita lo que pasa siempre
/// en estos paneles: un segundo motor de consulta, paralelo al de los informes, que con el tiempo
/// da números distintos para la misma pregunta y nadie sabe cuál creer.
///
/// La consecuencia práctica, que es lo bueno: cualquier widget se puede abrir en el constructor de
/// informes y ajustar, y cualquier informe se puede poner en el panel.
/// </summary>
/// <param name="Forma">
/// Cómo se pinta aquí, si se quiere distinto de como lo guardó el informe. El mismo informe puede
/// verse como tarta en un panel y como tabla en otro sin duplicarlo.
/// </param>
/// <param name="Titulo">
/// El título dentro del panel, si se quiere distinto del nombre del informe. «Tickets por estado»
/// puede llamarse «Cómo va el buzón» en el panel de quien lo mira cada mañana.
/// </param>
public sealed record Widget(
    Guid Id,
    Guid ReportId,
    int X,
    int Y,
    int Ancho,
    int Alto,
    string? Forma = null,
    string? Titulo = null)
{
    /// <summary>
    /// Columnas de la rejilla.
    ///
    /// Doce porque se divide bien: en dos, tres, cuatro y seis. Con diez, tres columnas iguales no
    /// existen y todo el mundo acaba dejando un hueco.
    /// </summary>
    public const int Columns = 12;

    /// <summary>Lo más ancho y lo más estrecho que puede ser un recuadro.</summary>
    public const int MinWidth = 3;

    /// <summary>Lo más bajo que puede ser sin que la gráfica deje de leerse.</summary>
    public const int MinHeight = 2;

    /// <summary>Tope de alto, para que un widget no empuje al resto fuera de la pantalla.</summary>
    public const int MaxHeight = 12;

    public Result Validate()
    {
        if (ReportId == Guid.Empty)
            return Result.Failure(Rules.MissingReport);

        if (Ancho < MinWidth || Ancho > Columns)
            return Result.Failure(Rules.WidthOutOfRange);

        if (Alto < MinHeight || Alto > MaxHeight)
            return Result.Failure(Rules.HeightOutOfRange);

        if (X < 0 || Y < 0)
            return Result.Failure(Rules.NegativePosition);

        // Un widget que empieza en la columna 10 y mide 4 se sale de la rejilla. En pantalla eso
        // se ve como un recuadro cortado o bajado de fila según el navegador, así que se rechaza
        // aquí en vez de dejar que cada uno lo dibuje a su manera.
        if (X + Ancho > Columns)
            return Result.Failure(Rules.OutsideGrid);

        return Result.Success();
    }

    public static class Rules
    {
        public const string MissingReport = "Un widget tiene que apuntar a un informe";
        public const string NegativePosition = "La posición no puede ser negativa";

        // Interpoladas con los límites reales para que el mensaje no se quede atrás si cambian.
        // `static readonly` y no `const`: una constante no puede interpolar otras.
        public static readonly string WidthOutOfRange = $"El ancho va de {MinWidth} a {Columns} columnas";
        public static readonly string HeightOutOfRange = $"El alto va de {MinHeight} a {MaxHeight} filas";
        public static readonly string OutsideGrid =
            $"El widget se sale de las {Columns} columnas de la rejilla";
    }
}
