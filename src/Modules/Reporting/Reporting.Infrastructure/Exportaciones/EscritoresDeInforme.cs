using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Reporting.Application.Exportaciones;

namespace Reporting.Infrastructure.Exportaciones;

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
public sealed class EscritorCsv : IEscritorDeInforme
{
    public string Formato => "Csv";
    public string TipoDeContenido => "text/csv; charset=utf-8";
    public string Extension => ".csv";

    private const char Separador = ';';

    public byte[] Escribir(TablaDeInforme tabla)
    {
        tabla.Validar();

        var texto = new StringBuilder();
        texto.AppendLine(string.Join(Separador, tabla.Columnas.Select(Escapar)));

        foreach (var fila in tabla.Filas)
            texto.AppendLine(string.Join(Separador, fila.Select(Escapar)));

        // El BOM se antepone a mano. Es lo que parece un detalle y no lo es: `GetBytes` **nunca**
        // escribe el preámbulo, por mucho que la codificación se construya con
        // `encoderShouldEmitUTF8Identifier: true` —esa opción sólo la mira un `StreamWriter`—.
        // Escrito de la forma evidente, el fichero salía sin BOM y Excel en Windows lo abría como
        // ANSI: «Diseño» se convertía en «DiseÃ±o». Lo cazó la prueba, no la lectura del código.
        var codificacion = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var contenido = codificacion.GetBytes(texto.ToString());

        return [.. Encoding.UTF8.GetPreamble(), .. contenido];
    }

    /// <summary>
    /// Entrecomilla lo que haga falta, y sólo eso.
    ///
    /// Un valor con separador, comillas o salto de línea va entre comillas, y las comillas de
    /// dentro se duplican, que es lo que dice el RFC 4180. Sin esto, una descripción de tarea
    /// con un punto y coma parte la fila y descuadra el resto del fichero.
    /// </summary>
    private static string Escapar(string? valor)
    {
        valor ??= string.Empty;

        var necesita = valor.Contains(Separador) || valor.Contains('"')
                       || valor.Contains('\n') || valor.Contains('\r');

        return necesita ? '"' + valor.Replace("\"", "\"\"") + '"' : valor;
    }
}

/// <summary>
/// El informe como hoja de cálculo.
///
/// El título va en las primeras filas y la tabla debajo con encabezado congelado y autofiltro,
/// que es lo que espera quien abre un informe en Excel: poder ordenar y filtrar sin preparar
/// nada.
/// </summary>
public sealed class EscritorExcel : IEscritorDeInforme
{
    public string Formato => "Excel";
    public string TipoDeContenido => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public string Extension => ".xlsx";

    /// <summary>
    /// Cuánto se deja ensanchar una columna al ajustarla al contenido.
    ///
    /// Sin tope, una descripción larga produce una columna de dos metros que deja el resto de la
    /// hoja fuera de la pantalla, y el fichero se abre inservible.
    /// </summary>
    private const double AnchoMaximo = 60;

    public byte[] Escribir(TablaDeInforme tabla)
    {
        tabla.Validar();

        using var libro = new XLWorkbook();

        // El nombre de la hoja tiene límites de Excel —31 caracteres y sin ciertos signos— y
        // pasarse hace que el fichero se abra «reparado» o no se abra. Se recorta aquí.
        var hoja = libro.Worksheets.Add(NombreDeHojaValido(tabla.Titulo));

        hoja.Cell(1, 1).Value = tabla.Titulo;
        hoja.Cell(1, 1).Style.Font.Bold = true;
        hoja.Cell(1, 1).Style.Font.FontSize = 14;

        var filaActual = 2;

        if (!string.IsNullOrWhiteSpace(tabla.Subtitulo))
        {
            hoja.Cell(filaActual, 1).Value = tabla.Subtitulo;
            hoja.Cell(filaActual, 1).Style.Font.FontColor = XLColor.Gray;
            filaActual++;
        }

        filaActual++; // Una fila en blanco entre la cabecera y la tabla.
        var filaEncabezado = filaActual;

        for (var c = 0; c < tabla.Columnas.Count; c++)
        {
            var celda = hoja.Cell(filaEncabezado, c + 1);
            celda.Value = tabla.Columnas[c];
            celda.Style.Font.Bold = true;
            celda.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF2FF");
        }

        for (var f = 0; f < tabla.Filas.Count; f++)
        {
            for (var c = 0; c < tabla.Columnas.Count; c++)
            {
                // Se escribe como texto a propósito. La tabla ya trae los valores formateados
                // para el idioma de quien lee, y dejar que Excel adivine el tipo convierte
                // referencias como «1-2» en fechas: es el clásico destrozo silencioso de los CSV
                // y aquí no se repite.
                hoja.Cell(filaEncabezado + 1 + f, c + 1).SetValue(tabla.Filas[f][c]);
            }
        }

        if (!tabla.EstaVacia)
        {
            var rango = hoja.Range(filaEncabezado, 1, filaEncabezado + tabla.Filas.Count, tabla.Columnas.Count);
            rango.SetAutoFilter();
        }

        hoja.SheetView.FreezeRows(filaEncabezado);
        hoja.Columns().AdjustToContents(1, AnchoMaximo);

        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        return memoria.ToArray();
    }

    private static string NombreDeHojaValido(string titulo)
    {
        var limpio = new string(titulo.Where(c => !"[]:*?/\\".Contains(c)).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(limpio)) limpio = "Informe";
        return limpio.Length > 31 ? limpio[..31] : limpio;
    }
}

/// <summary>
/// El informe como PDF.
///
/// <b>Se genera con QuestPDF y no con un navegador sin cabeza.</b> La alternativa habitual
/// —renderizar HTML con Chrome— obliga a meter un navegador entero en el contenedor de la API,
/// con sus dependencias del sistema, sus actualizaciones de seguridad y su consumo de memoria.
/// Para una tabla, eso es traer una imprenta para escribir una nota.
///
/// La licencia comunitaria de QuestPDF hay que declararla; se hace en <c>Program.cs</c> al
/// arrancar, no aquí, porque es una decisión de la aplicación y no de este escritor.
/// </summary>
public sealed class EscritorPdf : IEscritorDeInforme
{
    public string Formato => "Pdf";
    public string TipoDeContenido => "application/pdf";
    public string Extension => ".pdf";

    /// <summary>
    /// A partir de cuántas columnas se pasa a horizontal.
    ///
    /// Una tabla de ocho columnas en vertical deja el texto en tiras de dos palabras. Es
    /// preferible girar la hoja que entregar algo que no se lee.
    /// </summary>
    private const int ColumnasParaApaisar = 5;

    public byte[] Escribir(TablaDeInforme tabla)
    {
        tabla.Validar();

        var documento = Document.Create(contenedor =>
        {
            contenedor.Page(pagina =>
            {
                pagina.Size(tabla.Columnas.Count > ColumnasParaApaisar
                    ? PageSizes.A4.Landscape()
                    : PageSizes.A4);

                pagina.Margin(1.5f, Unit.Centimetre);
                pagina.DefaultTextStyle(x => x.FontSize(9));

                pagina.Header().Column(cabecera =>
                {
                    cabecera.Item().Text(tabla.Titulo).FontSize(16).SemiBold();

                    if (!string.IsNullOrWhiteSpace(tabla.Subtitulo))
                        cabecera.Item().Text(tabla.Subtitulo).FontSize(9).FontColor(Colors.Grey.Darken1);

                    cabecera.Item().PaddingTop(8);
                });

                pagina.Content().Table(cuadro =>
                {
                    cuadro.ColumnsDefinition(columnas =>
                    {
                        foreach (var _ in tabla.Columnas)
                            columnas.RelativeColumn();
                    });

                    // El encabezado se declara como tal para que QuestPDF lo repita en cada
                    // página. Sin eso, a partir de la segunda hoja las columnas no tienen nombre
                    // y hay que volver a la primera para saber qué se está mirando.
                    cuadro.Header(encabezado =>
                    {
                        foreach (var columna in tabla.Columnas)
                        {
                            encabezado.Cell()
                                .Background(Colors.Grey.Lighten3)
                                .Padding(4)
                                .Text(columna).SemiBold();
                        }
                    });

                    foreach (var fila in tabla.Filas)
                    {
                        foreach (var celda in fila)
                        {
                            cuadro.Cell()
                                .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                .Padding(4)
                                .Text(celda);
                        }
                    }
                });

                // Un informe vacío lo dice. Sin este aviso son dos páginas de encabezados y
                // nada más, y quien lo recibe no sabe si es que no hay datos o si falló algo.
                if (tabla.EstaVacia)
                {
                    pagina.Footer().PaddingTop(10)
                        .Text("No hay datos para este informe.")
                        .FontColor(Colors.Grey.Darken1).Italic();
                }
                else
                {
                    pagina.Footer().AlignRight().Text(texto =>
                    {
                        texto.CurrentPageNumber();
                        texto.Span(" de ");
                        texto.TotalPages();
                    });
                }
            });
        });

        return documento.GeneratePdf();
    }
}

/// <summary>
/// Elige el escritor según el formato pedido.
///
/// Falla con nombre si llega un formato sin escritor, en vez de devolver nulo: un nulo aquí se
/// convertiría, tres capas más allá, en «la exportación falló» sin más explicación.
/// </summary>
public sealed class EscritoresDeInforme(IEnumerable<IEscritorDeInforme> escritores)
{
    public IEscritorDeInforme Para(string formato)
        => escritores.FirstOrDefault(e => string.Equals(e.Formato, formato, StringComparison.OrdinalIgnoreCase))
           ?? throw new InvalidOperationException(
               $"No hay escritor para el formato «{formato}». Los que hay: "
               + string.Join(", ", escritores.Select(e => e.Formato)));
}
