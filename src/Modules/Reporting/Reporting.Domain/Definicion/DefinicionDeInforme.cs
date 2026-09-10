using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Domain;

namespace Reporting.Domain.Definicion;

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
/// <see cref="CatalogoDeInformes"/> y el motor la traduce a LINQ con un <c>switch</c>: lo que no
/// está en el catálogo, no existe.
/// </summary>
public sealed record DefinicionDeInforme(
    string Origen,
    string Agrupacion,
    string Medida,
    string Forma,
    IReadOnlyList<FiltroDeInforme>? Filtros = null,
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
    public IReadOnlyList<FiltroDeInforme> FiltrosAplicados => Filtros ?? [];

    /// <summary>
    /// Cuántos grupos caben en una gráfica antes de dejar de leerse.
    ///
    /// Una tarta de cuarenta porciones no comunica nada, y una lista de responsables en una
    /// empresa mediana las tiene. Cuando se pasa, el motor junta la cola en «Otros» **y lo dice**,
    /// en vez de recortar en silencio.
    /// </summary>
    public const int GruposPorDefecto = 20;

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
    public Result Validar()
    {
        var origen = CatalogoDeInformes.Origen(Origen);
        if (origen is null)
        {
            return Result.Failure(
                $"El origen «{Origen}» no existe. Los que hay: "
                + string.Join(", ", CatalogoDeInformes.Origenes().Select(o => o.Clave)));
        }

        var agrupacion = origen.Campo(Agrupacion);
        if (agrupacion is null)
        {
            return Result.Failure(
                $"«{Agrupacion}» no es un campo de {origen.Nombre}. Los que hay: "
                + string.Join(", ", origen.Campos.Select(c => c.Clave)));
        }

        if (origen.Medida(Medida) is null)
        {
            return Result.Failure(
                $"«{Medida}» no es una medida de {origen.Nombre}. Las que hay: "
                + string.Join(", ", origen.Medidas.Select(m => m.Clave)));
        }

        if (CatalogoDeInformes.Formas().All(f => f.Clave != Forma))
        {
            return Result.Failure(
                $"«{Forma}» no es una forma de pintar. Las que hay: "
                + string.Join(", ", CatalogoDeInformes.Formas().Select(f => f.Clave)));
        }

        // La granularidad sólo se pide si se agrupa por una fecha. Aceptarla en otros casos
        // dejaría guardado un dato que nadie mira y que confunde al leer la definición.
        if (agrupacion.Tipo == TipoDeCampo.Fecha)
        {
            if (Granularidad is null)
                return Result.Failure($"Agrupar por «{agrupacion.Nombre}» necesita decir si es por día, semana, mes o año");

            if (CatalogoDeInformes.GranularidadesDeFecha().All(g => g.Clave != Granularidad))
            {
                return Result.Failure(
                    $"«{Granularidad}» no es una granularidad. Las que hay: "
                    + string.Join(", ", CatalogoDeInformes.GranularidadesDeFecha().Select(g => g.Clave)));
            }
        }

        foreach (var filtro in FiltrosAplicados)
        {
            var resultado = filtro.Validar(origen);
            if (resultado.IsFailure) return resultado;
        }

        if (MaximoDeGrupos is <= 0)
            return Result.Failure("El máximo de grupos tiene que ser mayor que cero");

        return Result.Success();
    }

    /// <summary>
    /// Cuántos grupos se van a enseñar de verdad.
    ///
    /// Marcada, como <see cref="FiltrosAplicados"/>, para que no salga en el JSON: son atajos de
    /// lectura, no parte de lo que el usuario configuró. Colándose en la respuesta hacían que lo
    /// que se lee no fuera exactamente lo que se guarda, y quien reenviara ese JSON mandaría
    /// campos que el servidor ignora.
    /// </summary>
    [JsonIgnore]
    public int GruposEfectivos => MaximoDeGrupos ?? GruposPorDefecto;

    public string ASerializar() => JsonSerializer.Serialize(this, Json);

    /// <summary>
    /// Lee una definición guardada.
    ///
    /// Devuelve <c>null</c> si el JSON no es una definición, en vez de lanzar: una fila con datos
    /// viejos o corruptos no debe tumbar el listado de informes, sólo no ofrecerse como
    /// construible.
    /// </summary>
    public static DefinicionDeInforme? Leer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return JsonSerializer.Deserialize<DefinicionDeInforme>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Una condición sobre un campo.</summary>
public sealed record FiltroDeInforme(string Campo, string Operador, string? Valor)
{
    public Result Validar(OrigenDeDatos origen)
    {
        var campo = origen.Campo(Campo);
        if (campo is null)
        {
            return Result.Failure(
                $"«{Campo}» no es un campo de {origen.Nombre}. Los que hay: "
                + string.Join(", ", origen.Campos.Select(c => c.Clave)));
        }

        var operador = CatalogoDeInformes.Operador(Operador);
        if (operador is null)
        {
            return Result.Failure(
                $"«{Operador}» no es un operador. Los que hay: "
                + string.Join(", ", CatalogoDeInformes.Operadores().Select(o => o.Clave)));
        }

        // Se comprueba contra los operadores de **ese campo**, no sólo de su tipo. «Mayor que»
        // sobre un estado no significa nada, y «está vacío» sobre un campo que siempre tiene
        // valor tampoco: el segundo se colaba mirando sólo el tipo, y el filtro reventaba al
        // ejecutarse.
        if (CatalogoDeInformes.OperadoresPara(campo).All(o => o.Clave != operador.Clave))
        {
            return Result.Failure(
                $"«{operador.Nombre}» no se puede aplicar a {campo.Nombre}"
                + (operador.EsDeVacuidad ? ", que siempre tiene valor" : $", que es de tipo {campo.Tipo}"));
        }

        if (operador.NecesitaValor && string.IsNullOrWhiteSpace(Valor))
            return Result.Failure($"«{operador.Nombre}» sobre {campo.Nombre} necesita un valor");

        return Result.Success();
    }
}
