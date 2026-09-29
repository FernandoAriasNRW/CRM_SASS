namespace Reporting.Application.Exports;

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
/// <param name="Title">Encabeza el documento. En el CSV no aparece: ver <c>EscritorCsv</c>.</param>
/// <param name="Subtitle">Contexto: el periodo, los filtros aplicados. Puede faltar.</param>
/// <param name="Columns">Los encabezados, en orden.</param>
/// <param name="Rows">
/// Cada fila con tantas celdas como columnas. Quien la construye responde de que cuadren;
/// <see cref="Validate"/> lo comprueba antes de escribir nada.
/// </param>
public sealed record ReportTable(
    string Title,
    string? Subtitle,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows)
{
    /// <summary>
    /// Comprueba que la tabla es coherente antes de escribirla.
    ///
    /// Se hace aquí y no dentro de cada escritor porque los tres fallarían de forma distinta: el
    /// CSV escribiría una fila corta que abre el fichero desalineado a partir de ahí, ClosedXML
    /// lanzaría, y el PDF pintaría una celda vacía. Un fallo temprano y con nombre es más útil
    /// que tres síntomas.
    /// </summary>
    public void Validate()
    {
        if (Columns.Count == 0)
            throw new InvalidOperationException("Un informe sin columnas no se puede escribir");

        for (var i = 0; i < Rows.Count; i++)
        {
            if (Rows[i].Count != Columns.Count)
            {
                throw new InvalidOperationException(
                    $"La fila {i + 1} tiene {Rows[i].Count} celdas y hay {Columns.Count} columnas");
            }
        }
    }

    /// <summary>Una tabla sin filas. No es un error: es un informe que no encontró datos.</summary>
    public bool IsEmpty => Rows.Count == 0;
}
