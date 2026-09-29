using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Domain;

namespace Reporting.Domain.Definitions;

/// <summary>
/// Lo que un informe a medida pide: origen, filtros, agrupación, medida y forma.
///
/// <b>Es neutra a propósito.</b> No guarda opciones de ECharts ni SQL: guarda la intención. Es la
/// decisión que dejó escrita el plan —«guardar opciones de ECharts ataría todos los reportes
/// guardados a la librería: cambiarla algún día invalidaría el trabajo de los usuarios, no sólo
/// el nuestro»—. La misma definición la traduce el navegador a una gráfica y el servidor a un
/// fichero, y por eso los dos enseñan lo mismo.
///
/// <b>Y no guarda SQL.</b> La tentación en un constructor de informes es guardar un fragmento de
/// consulta, y entonces el informe que alguien construya se convierte en una entrada de
/// ejecución de código en la base de datos. Aquí cada pieza se valida contra
/// <see cref="ReportCatalog"/> y el motor la traduce a LINQ con un <c>switch</c>: lo que no
/// está en el catálogo, no existe.
/// </summary>
public sealed record ReportDefinition(
    string Origen,
    string Agrupacion,
    string Medida,
    string Forma,
    IReadOnlyList<ReportFilter>? Filtros = null,
    /// <summary>Cómo se agrupa la fecha, si la agrupación es por una. Ver el catálogo.</summary>
    string? Granularidad = null,
    /// <summary>Cuántos grupos como mucho; el resto se resume en «Otros». Nulo, todos.</summary>
    int? MaximoDeGrupos = null)
{
    /// <summary>
    /// Los filtros, nunca nulos.
    ///
    /// <b>Calculada y no con inicializador</b>, y la diferencia no es de estilo: escrita como
    /// <c>{ get; } = Filtros ?? []</c>, el <c>with</c> de los records copia el campo de respaldo
    /// del original en lugar de volver a ejecutar el inicializador. Una definición modificada con
    /// <c>with { Filtros = ... }</c> se quedaba con la lista vieja —vacía— y <b>sus filtros no se
    /// validaban ni se aplicaban</b>: el informe salía con todas las filas y sin dar ningún error.
    /// Lo cazó una prueba, no la lectura del código.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<ReportFilter> AppliedFilters => Filtros ?? [];

    /// <summary>
    /// Cuántos grupos caben en una gráfica antes de dejar de leerse.
    ///
    /// Una tarta de cuarenta porciones no comunica nada, y una lista de responsables en una
    /// empresa mediana las tiene. Cuando se pasa, el motor junta la cola en «Otros» **y lo dice**,
    /// en vez de recortar en silencio.
    /// </summary>
    public const int DefaultGroups = 20;

    /// <summary>Opciones de serialización compartidas: el JSON guardado y el leído son el mismo.</summary>
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Comprueba que todo lo que pide existe y encaja, y dice qué falla.
    ///
    /// Un mensaje por pieza, con el valor que se pidió y los que valen. El módulo ya cometió el
    /// error contrario —«Invalid report type or format» para dos campos distintos— y quien lo
    /// recibía no sabía cuál cambiar.
    /// </summary>
    public Result Validate()
    {
        var dataSource = ReportCatalog.FindDataSource(Origen);
        if (dataSource is null)
        {
            return Result.Failure(
                $"El origen «{Origen}» no existe. Los que hay: "
                + string.Join(", ", ReportCatalog.DataSources().Select(o => o.Key)));
        }

        var groupBy = dataSource.Field(Agrupacion);
        if (groupBy is null)
        {
            return Result.Failure(
                $"«{Agrupacion}» no es un campo de {dataSource.Name}. Los que hay: "
                + string.Join(", ", dataSource.Fields.Select(c => c.Key)));
        }

        if (dataSource.Measure(Medida) is null)
        {
            return Result.Failure(
                $"«{Medida}» no es una medida de {dataSource.Name}. Las que hay: "
                + string.Join(", ", dataSource.Measures.Select(m => m.Key)));
        }

        if (ReportCatalog.Visualizations().All(f => f.Key != Forma))
        {
            return Result.Failure(
                $"«{Forma}» no es una forma de pintar. Las que hay: "
                + string.Join(", ", ReportCatalog.Visualizations().Select(f => f.Key)));
        }

        // La granularidad sólo se pide si se agrupa por una fecha. Aceptarla en otros casos
        // dejaría guardado un dato que nadie mira y que confunde al leer la definición.
        if (groupBy.Type == FieldType.Date)
        {
            if (Granularidad is null)
                return Result.Failure($"Agrupar por «{groupBy.Name}» necesita decir si es por día, semana, mes o año");

            if (ReportCatalog.DateGranularities().All(g => g.Key != Granularidad))
            {
                return Result.Failure(
                    $"«{Granularidad}» no es una granularidad. Las que hay: "
                    + string.Join(", ", ReportCatalog.DateGranularities().Select(g => g.Key)));
            }
        }

        foreach (var filter in AppliedFilters)
        {
            var result = filter.Validate(dataSource);
            if (result.IsFailure) return result;
        }

        if (MaximoDeGrupos is <= 0)
            return Result.Failure("El máximo de grupos tiene que ser mayor que cero");

        return Result.Success();
    }

    /// <summary>
    /// Cuántos grupos se van a enseñar de verdad.
    ///
    /// Marcada, como <see cref="AppliedFilters"/>, para que no salga en el JSON: son atajos de
    /// lectura, no parte de lo que el usuario configuró. Colándose en la respuesta hacían que lo
    /// que se lee no fuera exactamente lo que se guarda, y quien reenviara ese JSON mandaría
    /// campos que el servidor ignora.
    /// </summary>
    [JsonIgnore]
    public int EffectiveGroups => MaximoDeGrupos ?? DefaultGroups;

    public string Serialize() => JsonSerializer.Serialize(this, Json);

    /// <summary>
    /// Lee una definición guardada.
    ///
    /// Devuelve <c>null</c> si el JSON no es una definición, en vez de lanzar: una fila con datos
    /// viejos o corruptos no debe tumbar el listado de informes, sólo no ofrecerse como
    /// construible.
    /// </summary>
    public static ReportDefinition? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return JsonSerializer.Deserialize<ReportDefinition>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
