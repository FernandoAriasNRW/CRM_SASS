using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Domain;

namespace Reporting.Domain.Paneles;

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
    public const int Columnas = 12;

    /// <summary>Lo más ancho y lo más estrecho que puede ser un recuadro.</summary>
    public const int AnchoMinimo = 3;

    /// <summary>Lo más bajo que puede ser sin que la gráfica deje de leerse.</summary>
    public const int AltoMinimo = 2;

    /// <summary>Tope de alto, para que un widget no empuje al resto fuera de la pantalla.</summary>
    public const int AltoMaximo = 12;

    public Result Validar()
    {
        if (ReportId == Guid.Empty)
            return Result.Failure(Reglas.SinInforme);

        if (Ancho < AnchoMinimo || Ancho > Columnas)
            return Result.Failure(Reglas.AnchoFuera);

        if (Alto < AltoMinimo || Alto > AltoMaximo)
            return Result.Failure(Reglas.AltoFuera);

        if (X < 0 || Y < 0)
            return Result.Failure(Reglas.PosicionNegativa);

        // Un widget que empieza en la columna 10 y mide 4 se sale de la rejilla. En pantalla eso
        // se ve como un recuadro cortado o bajado de fila según el navegador, así que se rechaza
        // aquí en vez de dejar que cada uno lo dibuje a su manera.
        if (X + Ancho > Columnas)
            return Result.Failure(Reglas.SeSaleDeLaRejilla);

        return Result.Success();
    }

    public static class Reglas
    {
        public const string SinInforme = "Un widget tiene que apuntar a un informe";
        public const string PosicionNegativa = "La posición no puede ser negativa";

        // Interpoladas con los límites reales para que el mensaje no se quede atrás si cambian.
        // `static readonly` y no `const`: una constante no puede interpolar otras.
        public static readonly string AnchoFuera = $"El ancho va de {AnchoMinimo} a {Columnas} columnas";
        public static readonly string AltoFuera = $"El alto va de {AltoMinimo} a {AltoMaximo} filas";
        public static readonly string SeSaleDeLaRejilla =
            $"El widget se sale de las {Columnas} columnas de la rejilla";
    }
}

/// <summary>
/// Los widgets de un panel, con su colocación.
///
/// Se guarda serializada en el propio panel porque siempre se lee entera —para pintar el panel
/// hacen falta todos los widgets— y nunca se consulta por partes. Una tabla de widgets sería una
/// unión más en cada carga sin ganar nada.
/// </summary>
public sealed record DisposicionDelPanel(IReadOnlyList<Widget>? Widgets = null)
{
    /// <summary>
    /// Cuántos widgets caben en un panel.
    ///
    /// Cada uno es una consulta al abrir la pantalla. Sin tope, un panel de cien recuadros tarda
    /// medio minuto en cargar y quien lo montó no relaciona una cosa con la otra.
    /// </summary>
    public const int MaximoDeWidgets = 24;

    /// <summary>
    /// Los widgets, nunca nulos.
    ///
    /// Calculada y no con inicializador: con <c>{ get; } = Widgets ?? []</c>, el <c>with</c> de
    /// los records copia el campo de respaldo del original en vez de recalcularlo. Ya mordió una
    /// vez en la definición de informes, donde dejó los filtros sin validar ni aplicar.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<Widget> Colocados => Widgets ?? [];

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public Result Validar()
    {
        if (Colocados.Count > MaximoDeWidgets)
            return Result.Failure($"Un panel admite {MaximoDeWidgets} recuadros como mucho");

        // Dos widgets con el mismo identificador harían que mover uno moviera los dos, y que
        // borrar uno borrara el otro. Es el tipo de fallo que se achaca al navegador.
        var repetidos = Colocados.GroupBy(w => w.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (repetidos.Count > 0)
            return Result.Failure($"Hay widgets repetidos en el panel: {string.Join(", ", repetidos)}");

        foreach (var widget in Colocados)
        {
            var resultado = widget.Validar();
            if (resultado.IsFailure) return resultado;
        }

        return Result.Success();
    }

    public string ASerializar() => JsonSerializer.Serialize(this, Json);

    /// <summary>
    /// Lee una disposición guardada.
    ///
    /// Devuelve una vacía —no nula, ni lanza— si el JSON no es una disposición: un panel con datos
    /// viejos se abre vacío y se puede volver a montar, en vez de reventar la pantalla entera.
    /// </summary>
    public static DisposicionDelPanel Leer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new DisposicionDelPanel();

        try
        {
            return JsonSerializer.Deserialize<DisposicionDelPanel>(json, Json) ?? new DisposicionDelPanel();
        }
        catch (JsonException)
        {
            return new DisposicionDelPanel();
        }
    }
}
