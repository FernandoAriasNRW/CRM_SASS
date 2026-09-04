using System.Text;
using FluentAssertions;
using Reporting.Application.Exportaciones;
using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;
using Reporting.Infrastructure.Exportaciones;
using Xunit;

namespace UnitTests;

/// <summary>
/// El ciclo de vida de una exportación.
///
/// Lo que se vigila aquí no es que los estados cambien —eso es evidente leyendo la clase— sino
/// las dos cosas que el plan señalaba como las que pudren estos sistemas: que nada se quede en
/// «generando» para siempre, y que un fallo diga por qué.
/// </summary>
public class ExportacionTests
{
    private static Exportacion Nueva()
        => Exportacion.Solicitar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ReportFormat.Csv).Value!;

    [Fact]
    public void Nace_pendiente()
    {
        var exportacion = Nueva();

        exportacion.Estado.Should().Be(EstadoDeExportacion.Pendiente);
        exportacion.Intentos.Should().Be(0);
        exportacion.NombreDeFichero.Should().BeNull();
    }

    [Fact]
    public void Sin_solicitante_no_se_puede_pedir()
    {
        var resultado = Exportacion.Solicitar(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, ReportFormat.Pdf);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Be(Exportacion.Reglas.SinSolicitante);
    }

    /// <summary>Dos trabajadores compitiendo: el segundo se va con las manos vacías, sin error.</summary>
    [Fact]
    public void Solo_un_trabajador_se_la_lleva()
    {
        var exportacion = Nueva();

        exportacion.Comenzar().Should().BeTrue();
        exportacion.Comenzar().Should().BeFalse("otro ya la cogió; no es un error, es una carrera perdida");

        exportacion.Intentos.Should().Be(1, "el que pierde no cuenta como intento");
    }

    [Fact]
    public void Al_terminar_queda_lista_con_su_fichero()
    {
        var exportacion = Nueva();
        exportacion.Comenzar();

        exportacion.Terminar("informe.csv", 1234);

        exportacion.Estado.Should().Be(EstadoDeExportacion.Lista);
        exportacion.NombreDeFichero.Should().Be("informe.csv");
        exportacion.TamanoBytes.Should().Be(1234);
        exportacion.Error.Should().BeNull();
    }

    [Fact]
    public void Al_fallar_queda_el_motivo()
    {
        var exportacion = Nueva();
        exportacion.Comenzar();

        exportacion.Fallar("La base de datos no responde");

        exportacion.Estado.Should().Be(EstadoDeExportacion.Fallida);
        exportacion.Error.Should().Be("La base de datos no responde");
    }

    /// <summary>
    /// Un fallo siempre deja explicación, aunque quien lo provoque no dé ninguna.
    ///
    /// Una excepción con mensaje vacío existe —las hay— y sin esto la pantalla enseñaría
    /// «Fallida:» y nada más, que es lo mismo que no decir nada.
    /// </summary>
    [Fact]
    public void Un_fallo_sin_mensaje_igualmente_explica_algo()
    {
        var exportacion = Nueva();
        exportacion.Comenzar();

        exportacion.Fallar("   ");

        exportacion.Error.Should().Be(Exportacion.Reglas.FalloSinExplicacion);
    }

    [Fact]
    public void Terminar_levanta_el_evento_que_dispara_el_aviso()
    {
        var exportacion = Nueva();
        exportacion.Comenzar();

        exportacion.Terminar("informe.pdf", 10);

        exportacion.DomainEvents.Should().ContainSingle(e => e is Reporting.Domain.Events.ExportacionListaEvent);
    }

    [Fact]
    public void Fallar_tambien_levanta_evento_porque_del_fallo_tambien_se_avisa()
    {
        var exportacion = Nueva();
        exportacion.Comenzar();

        exportacion.Fallar("se rompió");

        exportacion.DomainEvents.Should().ContainSingle(e => e is Reporting.Domain.Events.ExportacionFallidaEvent);
    }

    [Fact]
    public void Una_recien_empezada_no_se_vuelve_a_coger()
    {
        var exportacion = Nueva();
        exportacion.Comenzar();

        exportacion.PuedeComenzar().Should().BeFalse(
            "acaba de empezar; recogerla ahora sería generarla dos veces a la vez");
    }

    [Fact]
    public void Una_lista_no_se_vuelve_a_generar()
    {
        var exportacion = Nueva();
        exportacion.Comenzar();
        exportacion.Terminar("x.csv", 1);

        exportacion.PuedeComenzar().Should().BeFalse();
    }

    [Fact]
    public void Reencolar_la_devuelve_a_la_cola()
    {
        var exportacion = Nueva();
        exportacion.Comenzar();
        exportacion.Fallar("un fallo pasajero");

        exportacion.Reencolar();

        exportacion.Estado.Should().Be(EstadoDeExportacion.Pendiente);
        exportacion.PuedeComenzar().Should().BeTrue();
    }

    /// <summary>
    /// Se agotan los intentos y deja de reintentarse.
    ///
    /// Sin este tope, una exportación que falla siempre —un informe cuyo tipo no sabe generar
    /// datos, por ejemplo— se reintentaría cada cinco segundos para siempre, llenando el registro
    /// y consumiendo el trabajador que otros necesitan.
    /// </summary>
    [Fact]
    public void Los_intentos_se_acaban()
    {
        var exportacion = Nueva();

        for (var i = 0; i < Exportacion.IntentosMaximos; i++)
        {
            exportacion.Reencolar();
            exportacion.Comenzar().Should().BeTrue();
        }

        exportacion.Intentos.Should().Be(Exportacion.IntentosMaximos);
        exportacion.QuedanIntentos().Should().BeFalse();
    }
}

/// <summary>
/// Los tres escritores de fichero.
///
/// Son código puro —tabla entra, bytes salen— así que se prueban sin base de datos. Lo que se
/// comprueba es lo que rompe un fichero en manos de quien lo abre: el escapado, el BOM y la
/// coherencia entre columnas y filas.
/// </summary>
public class EscritoresDeInformeTests
{
    private static TablaDeInforme Tabla(params string[][] filas)
        => new("Informe de prueba", "Un subtítulo",
               ["Nombre", "Cantidad"],
               filas.Select(f => (IReadOnlyList<string>)f).ToList());

    #region CSV

    /// <summary>
    /// El BOM, que es lo que separa un CSV legible de uno con los acentos rotos.
    ///
    /// Sin él, Excel en Windows abre el fichero como ANSI y «Diseño» sale como «DiseÃ±o». Es el
    /// primer motivo por el que alguien dice que «la exportación no funciona».
    /// </summary>
    [Fact]
    public void El_csv_lleva_bom_para_que_los_acentos_no_se_rompan()
    {
        var bytes = new EscritorCsv().Escribir(Tabla(["Diseño", "3"]));

        bytes.Take(3).Should().Equal((byte)0xEF, (byte)0xBB, (byte)0xBF);
        Encoding.UTF8.GetString(bytes).Should().Contain("Diseño");
    }

    /// <summary>
    /// El separador es el punto y coma porque la coma es el separador decimal en español.
    ///
    /// Con comas, «1,5» acabaría partido en dos columnas.
    /// </summary>
    [Fact]
    public void El_csv_separa_por_punto_y_coma()
    {
        var bytes = new EscritorCsv().Escribir(Tabla(["Tarea", "1,5"]));
        var texto = Encoding.UTF8.GetString(bytes);

        texto.Should().Contain("Nombre;Cantidad");

        // «1,5» va **sin comillas**, y eso es exactamente lo que se busca: con la coma como
        // separador habría que entrecomillar cada número decimal del informe, y cualquier
        // herramienta que se saltara el entrecomillado partiría la fila. Con punto y coma, un
        // decimal español es un valor normal.
        texto.Should().Contain("Tarea;1,5");
    }

    /// <summary>
    /// Un valor con el separador dentro se entrecomilla, o parte la fila.
    ///
    /// Es el fallo clásico: una descripción con un punto y coma descuadra el fichero **a partir
    /// de ahí**, así que el síntoma aparece en filas que no tienen nada que ver.
    /// </summary>
    [Fact]
    public void El_csv_entrecomilla_lo_que_llevaria_a_partir_la_fila()
    {
        var bytes = new EscritorCsv().Escribir(Tabla(["Revisar; luego cerrar", "2"]));
        var texto = Encoding.UTF8.GetString(bytes);

        texto.Should().Contain("\"Revisar; luego cerrar\";2");
    }

    [Fact]
    public void El_csv_duplica_las_comillas_de_dentro()
    {
        var bytes = new EscritorCsv().Escribir(Tabla(["Dijo \"vale\"", "1"]));

        Encoding.UTF8.GetString(bytes).Should().Contain("\"Dijo \"\"vale\"\"\"");
    }

    [Fact]
    public void El_csv_entrecomilla_los_saltos_de_linea()
    {
        var bytes = new EscritorCsv().Escribir(Tabla(["Primera\nSegunda", "1"]));

        Encoding.UTF8.GetString(bytes).Should().Contain("\"Primera\nSegunda\"");
    }

    /// <summary>
    /// El CSV no lleva título.
    ///
    /// Una línea de título antes de los encabezados desplaza toda la tabla: cualquier herramienta
    /// que lo abra tomará el título como el nombre de la primera columna.
    /// </summary>
    [Fact]
    public void El_csv_empieza_por_los_encabezados_y_no_por_el_titulo()
    {
        var bytes = new EscritorCsv().Escribir(Tabla(["Algo", "1"]));
        var primera = Encoding.UTF8.GetString(bytes).Split('\n')[0].TrimStart('﻿');

        primera.Should().StartWith("Nombre;Cantidad");
    }

    #endregion

    #region Coherencia

    /// <summary>
    /// Una fila con menos celdas que columnas falla **antes** de escribir nada, y con nombre.
    ///
    /// Se comprueba en la tabla y no dentro de cada escritor porque los tres fallarían distinto:
    /// el CSV escribiría una fila corta que desalinea el fichero de ahí en adelante, ClosedXML
    /// lanzaría y el PDF pintaría una celda vacía. Tres síntomas para una sola causa.
    /// </summary>
    [Fact]
    public void Una_fila_descuadrada_se_detecta_antes_de_escribir()
    {
        var tabla = new TablaDeInforme("X", null, ["A", "B"], [["solo una"]]);

        var accion = () => new EscritorCsv().Escribir(tabla);

        accion.Should().Throw<InvalidOperationException>()
            .WithMessage("*1 celdas*2 columnas*");
    }

    [Fact]
    public void Una_tabla_sin_columnas_no_se_escribe()
    {
        var tabla = new TablaDeInforme("X", null, [], []);

        var accion = () => new EscritorCsv().Escribir(tabla);

        accion.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Excel y PDF

    /// <summary>
    /// El Excel sale y es un fichero de verdad.
    ///
    /// Se comprueba la firma del ZIP —un `.xlsx` es un zip— en lugar de abrirlo con la propia
    /// librería: comprobar un fichero con la misma librería que lo escribió no descubre gran
    /// cosa.
    /// </summary>
    [Fact]
    public void El_excel_es_un_xlsx_valido()
    {
        var bytes = new EscritorExcel().Escribir(Tabla(["Algo", "1"], ["Otra cosa", "2"]));

        bytes.Length.Should().BeGreaterThan(0);
        // Un .xlsx es un zip: empieza por «PK».
        bytes.Take(2).Should().Equal((byte)0x50, (byte)0x4B);
    }

    [Fact]
    public void El_pdf_es_un_pdf_valido()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var bytes = new EscritorPdf().Escribir(Tabla(["Algo", "1"]));

        Encoding.ASCII.GetString(bytes.Take(5).ToArray()).Should().Be("%PDF-");
    }

    /// <summary>
    /// Un informe sin datos genera fichero igual, y no un error.
    ///
    /// «No hay datos» es una respuesta legítima de un informe. Fallar aquí obligaría a quien lo
    /// pidió a preguntarse si se rompió algo.
    /// </summary>
    [Fact]
    public void Un_informe_vacio_tambien_produce_fichero()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var vacia = new TablaDeInforme("Sin datos", null, ["A"], []);

        new EscritorCsv().Escribir(vacia).Length.Should().BeGreaterThan(0);
        new EscritorExcel().Escribir(vacia).Length.Should().BeGreaterThan(0);
        new EscritorPdf().Escribir(vacia).Length.Should().BeGreaterThan(0);
    }

    #endregion

    #region El selector

    [Fact]
    public void El_selector_devuelve_el_escritor_de_cada_formato()
    {
        var selector = new EscritoresDeInforme([new EscritorCsv(), new EscritorExcel(), new EscritorPdf()]);

        selector.Para("Csv").Extension.Should().Be(".csv");
        selector.Para("Excel").Extension.Should().Be(".xlsx");
        selector.Para("Pdf").Extension.Should().Be(".pdf");
    }

    /// <summary>
    /// Un formato sin escritor falla diciendo cuáles hay.
    ///
    /// Devolver nulo convertiría esto, tres capas más allá, en «la exportación falló» sin
    /// explicación: justo lo que la pantalla de informes ya hizo una vez con los tipos.
    /// </summary>
    [Fact]
    public void Un_formato_sin_escritor_lo_dice_y_enumera_los_que_hay()
    {
        var selector = new EscritoresDeInforme([new EscritorCsv()]);

        var accion = () => selector.Para("Word");

        accion.Should().Throw<InvalidOperationException>().WithMessage("*Word*Csv*");
    }

    #endregion
}
