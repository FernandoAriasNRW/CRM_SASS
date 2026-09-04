namespace Reporting.Application.Exportaciones;

/// <summary>
/// Un informe ya resuelto, en la forma neutra que entienden los tres escritores.
///
/// <b>Existe para que haya un solo sitio donde se decide qué dice un informe.</b> Sin ella, cada
/// formato consultaría los datos por su cuenta y el PDF y el Excel del mismo informe acabarían
/// discrepando —normalmente en los totales, que es donde más duele—. Aquí el dato se calcula una
/// vez y los formatos sólo deciden cómo se dibuja.
///
/// Es deliberadamente pobre: columnas y filas de texto. La alternativa era un modelo tipado con
/// números, fechas y monedas, y no sale a cuenta: el formateo depende del idioma de quien lee y
/// eso ya se resuelve al construir la tabla, mientras que un modelo tipado obligaría a los tres
/// escritores a repetir las mismas reglas de formato.
/// </summary>
/// <param name="Titulo">Encabeza el documento. En el CSV no aparece: ver <c>EscritorCsv</c>.</param>
/// <param name="Subtitulo">Contexto: el periodo, los filtros aplicados. Puede faltar.</param>
/// <param name="Columnas">Los encabezados, en orden.</param>
/// <param name="Filas">
/// Cada fila con tantas celdas como columnas. Quien la construye responde de que cuadren;
/// <see cref="Validar"/> lo comprueba antes de escribir nada.
/// </param>
public sealed record TablaDeInforme(
    string Titulo,
    string? Subtitulo,
    IReadOnlyList<string> Columnas,
    IReadOnlyList<IReadOnlyList<string>> Filas)
{
    /// <summary>
    /// Comprueba que la tabla es coherente antes de escribirla.
    ///
    /// Se hace aquí y no dentro de cada escritor porque los tres fallarían de forma distinta: el
    /// CSV escribiría una fila corta que abre el fichero desalineado a partir de ahí, ClosedXML
    /// lanzaría, y el PDF pintaría una celda vacía. Un fallo temprano y con nombre es más útil
    /// que tres síntomas.
    /// </summary>
    public void Validar()
    {
        if (Columnas.Count == 0)
            throw new InvalidOperationException("Un informe sin columnas no se puede escribir");

        for (var i = 0; i < Filas.Count; i++)
        {
            if (Filas[i].Count != Columnas.Count)
            {
                throw new InvalidOperationException(
                    $"La fila {i + 1} tiene {Filas[i].Count} celdas y hay {Columnas.Count} columnas");
            }
        }
    }

    /// <summary>Una tabla sin filas. No es un error: es un informe que no encontró datos.</summary>
    public bool EstaVacia => Filas.Count == 0;
}

/// <summary>
/// Escribe una <see cref="TablaDeInforme"/> en un formato concreto.
///
/// Uno por formato, elegidos por <c>ReportFormat</c>. Devuelve bytes y no un <c>Stream</c>: el
/// resultado se guarda entero de todas formas, y un stream obligaría a cada llamante a acordarse
/// de cerrarlo.
/// </summary>
public interface IEscritorDeInforme
{
    /// <summary>El formato que sabe escribir, por nombre de <c>ReportFormat</c>.</summary>
    string Formato { get; }

    /// <summary>Lo que va en la cabecera HTTP al descargar.</summary>
    string TipoDeContenido { get; }

    /// <summary>La extensión, con punto.</summary>
    string Extension { get; }

    byte[] Escribir(TablaDeInforme tabla);
}
