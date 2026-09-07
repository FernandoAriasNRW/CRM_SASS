using FluentAssertions;
using Reporting.Domain.Definicion;
using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;
using Xunit;

namespace UnitTests;

/// <summary>
/// La definición de un informe a medida.
///
/// Lo que se vigila es que **nada pase sin estar en el catálogo**. Un constructor de informes
/// multiplica por veinte la superficie del fallo que este módulo ya cometió dos veces —ofrecer
/// opciones que el servidor no conoce—, con el agravante de que aquí el usuario guarda la
/// definición: el error aparecería al exportar, días después, cuando ya no está mirando.
/// </summary>
public class DefinicionDeInformeTests
{
    private static DefinicionDeInforme Valida() =>
        new(Origen: "Tareas", Agrupacion: "estado", Medida: "conteo", Forma: "barras");

    [Fact]
    public void Una_definicion_completa_vale()
    {
        Valida().Validar().IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Un_origen_que_no_existe_dice_cuales_hay()
    {
        var definicion = Valida() with { Origen = "Facturas" };

        var resultado = definicion.Validar();

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Contain("Facturas").And.Contain("Tareas");
    }

    [Fact]
    public void Un_campo_de_agrupacion_de_otro_origen_se_rechaza()
    {
        // «agente» es de tickets, no de tareas. Sin esta comprobación el motor caería en su
        // `default` al generar, que es cuando quien lo construyó ya no está delante.
        var definicion = Valida() with { Agrupacion = "agente" };

        var resultado = definicion.Validar();

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Contain("agente").And.Contain("estado");
    }

    [Fact]
    public void Una_medida_que_el_origen_no_tiene_se_rechaza()
    {
        // Los proyectos sólo se pueden contar; no tienen horas que sumar.
        var definicion = Valida() with { Origen = "Proyectos", Agrupacion = "estado", Medida = "suma_horas" };

        var resultado = definicion.Validar();

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Contain("suma_horas").And.Contain("conteo");
    }

    [Fact]
    public void Una_forma_inventada_se_rechaza()
    {
        var resultado = (Valida() with { Forma = "holograma" }).Validar();

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Contain("holograma");
    }

    /// <summary>
    /// Agrupar por una fecha exige decir la granularidad.
    ///
    /// Sin ella habría que elegir una por defecto, y la elección sería invisible: un informe
    /// «tareas por vencimiento» que sale con una barra por día parece roto, y quien lo mire no
    /// sabrá que podía pedir meses.
    /// </summary>
    [Fact]
    public void Agrupar_por_fecha_sin_granularidad_se_rechaza()
    {
        var resultado = (Valida() with { Agrupacion = "vencimiento" }).Validar();

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Contain("día, semana, mes o año");
    }

    [Fact]
    public void Agrupar_por_fecha_con_granularidad_vale()
    {
        var definicion = Valida() with { Agrupacion = "vencimiento", Granularidad = "mes" };

        definicion.Validar().IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Una_granularidad_inventada_se_rechaza()
    {
        var definicion = Valida() with { Agrupacion = "vencimiento", Granularidad = "quincena" };

        definicion.Validar().Error.Should().Contain("quincena");
    }

    #region Filtros

    [Fact]
    public void Un_filtro_sobre_un_campo_que_no_existe_se_rechaza()
    {
        var definicion = Valida() with { Filtros = [new FiltroDeInforme("color", "es", "azul")] };

        definicion.Validar().Error.Should().Contain("color");
    }

    /// <summary>
    /// El operador tiene que encajar con el tipo del campo.
    ///
    /// «Mayor que» sobre un estado no significa nada. Sin esta comprobación el motor devolvería
    /// una lista arbitraria —o lanzaría— y el informe parecería roto sin decir por qué.
    /// </summary>
    [Fact]
    public void Mayor_que_sobre_un_texto_se_rechaza()
    {
        var definicion = Valida() with { Filtros = [new FiltroDeInforme("estado", "mayor_que", "Done")] };

        var resultado = definicion.Validar();

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Contain("Mayor que").And.Contain("Estado");
    }

    [Fact]
    public void Contiene_sobre_una_persona_se_rechaza()
    {
        // Un responsable es un identificador; «contiene» sobre él sería buscar dentro de un GUID.
        var definicion = Valida() with { Filtros = [new FiltroDeInforme("responsable", "contiene", "ana")] };

        definicion.Validar().IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Un_operador_que_necesita_valor_sin_valor_se_rechaza()
    {
        var definicion = Valida() with { Filtros = [new FiltroDeInforme("estado", "es", null)] };

        definicion.Validar().Error.Should().Contain("necesita un valor");
    }

    /// <summary>«Está vacío» no necesita valor, y exigírselo sería un formulario imposible.</summary>
    [Fact]
    public void Vacio_no_necesita_valor()
    {
        var definicion = Valida() with
        {
            Origen = "Tickets",
            Agrupacion = "estado",
            Filtros = [new FiltroDeInforme("resolucion", "vacio", null)]
        };

        definicion.Validar().IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Varios_filtros_se_validan_todos()
    {
        var definicion = Valida() with
        {
            Filtros =
            [
                new FiltroDeInforme("estado", "es", "Done"),
                new FiltroDeInforme("prioridad", "mayor_que", "High")
            ]
        };

        // El primero vale y el segundo no: el resultado tiene que señalar al segundo.
        definicion.Validar().Error.Should().Contain("Prioridad");
    }

    #endregion

    #region Ida y vuelta

    /// <summary>
    /// Lo guardado se vuelve a leer igual.
    ///
    /// Importa porque la definición viaja a la base como JSON y vuelve para reabrir el
    /// constructor: si perdiera los filtros por el camino, el informe se seguiría generando —con
    /// otros datos— y nadie lo notaría hasta comparar cifras.
    /// </summary>
    [Fact]
    public void Una_definicion_sobrevive_a_guardarse_y_leerse()
    {
        var original = new DefinicionDeInforme(
            "Tickets", "creacion", "media_dias_resolucion", "lineas",
            [new FiltroDeInforme("prioridad", "es", "High")],
            Granularidad: "semana",
            MaximoDeGrupos: 5);

        var leida = DefinicionDeInforme.Leer(original.ASerializar());

        leida.Should().NotBeNull();
        leida!.Origen.Should().Be("Tickets");
        leida.Granularidad.Should().Be("semana");
        leida.MaximoDeGrupos.Should().Be(5);
        leida.FiltrosAplicados.Should().ContainSingle(f => f.Campo == "prioridad" && f.Valor == "High");
    }

    /// <summary>
    /// Un JSON corrupto devuelve nulo, no lanza.
    ///
    /// Una fila con datos viejos no puede tumbar el listado de informes; como mucho, ese informe
    /// no se ofrece como construible.
    /// </summary>
    [Fact]
    public void Un_json_que_no_es_una_definicion_no_revienta()
    {
        DefinicionDeInforme.Leer("{esto no es json").Should().BeNull();
        DefinicionDeInforme.Leer(null).Should().BeNull();
        DefinicionDeInforme.Leer("   ").Should().BeNull();
    }

    #endregion

    #region El catálogo

    /// <summary>
    /// Cada medida que dice calcularse sobre un campo, nombra un campo que existe en su origen.
    ///
    /// Es la clase de desajuste que no da error al guardar y revienta al generar: la medida
    /// «suma de horas» sobre un origen sin campo de horas.
    /// </summary>
    [Fact]
    public void Las_medidas_apuntan_a_campos_que_existen()
    {
        foreach (var origen in CatalogoDeInformes.Origenes())
        {
            foreach (var medida in origen.Medidas.Where(m => m.SobreElCampo is not null))
            {
                origen.Campo(medida.SobreElCampo).Should().NotBeNull(
                    $"la medida «{medida.Clave}» de {origen.Clave} dice calcularse sobre "
                    + $"«{medida.SobreElCampo}», que no es un campo de ese origen");
            }
        }
    }

    /// <summary>Todo origen se puede contar; sin eso no habría informe posible sobre él.</summary>
    [Fact]
    public void Todos_los_origenes_saben_contar()
    {
        foreach (var origen in CatalogoDeInformes.Origenes())
            origen.Medida("conteo").Should().NotBeNull($"{origen.Clave} tiene que poder contarse");
    }

    /// <summary>
    /// Todo campo tiene al menos un operador aplicable.
    ///
    /// Un campo sin operadores es un filtro que la pantalla ofrece y que no se puede completar.
    /// </summary>
    [Fact]
    public void Todo_campo_se_puede_filtrar_de_alguna_forma()
    {
        foreach (var origen in CatalogoDeInformes.Origenes())
        {
            foreach (var campo in origen.Campos)
            {
                CatalogoDeInformes.Operadores().Any(o => o.ValePara(campo.Tipo)).Should().BeTrue(
                    $"«{campo.Clave}» de {origen.Clave} es de tipo {campo.Tipo} y ningún operador lo admite");
            }
        }
    }

    #endregion
}

/// <summary>
/// Los informes programados.
///
/// <c>TocaAhora</c> es una función pura justamente para esto: los casos límite de un planificador
/// —el domingo, el día 1, el minuto antes de la hora— sólo se pueden comprobar de verdad si no
/// hace falta esperar a que llegue ese momento.
/// </summary>
public class ProgramacionDeInformeTests
{
    private static readonly Guid Inquilino = Guid.NewGuid();
    private static readonly Guid Informe = Guid.NewGuid();
    private static readonly Guid Persona = Guid.NewGuid();

    private static ProgramacionDeInforme Crear(
        FrecuenciaDeInforme frecuencia, string hora = "08:00", int? dia = null)
        => ProgramacionDeInforme.Crear(
            Inquilino, Informe, Persona, frecuencia, ReportFormat.Pdf, TimeOnly.Parse(hora), dia).Value!;

    /// <summary>Un lunes a las 9, para tener una referencia con nombre.</summary>
    private static readonly DateTime LunesALasNueve = new(2026, 9, 7, 9, 0, 0);

    [Fact]
    public void Nace_activa_y_sin_haberse_generado()
    {
        var programacion = Crear(FrecuenciaDeInforme.Diaria);

        programacion.Activa.Should().BeTrue();
        programacion.UltimoDiaGenerado.Should().BeNull();
    }

    #region Cuándo toca

    [Fact]
    public void La_diaria_toca_pasada_su_hora()
    {
        Crear(FrecuenciaDeInforme.Diaria, "08:00").TocaAhora(LunesALasNueve).Should().BeTrue();
    }

    [Fact]
    public void La_diaria_no_toca_antes_de_su_hora()
    {
        Crear(FrecuenciaDeInforme.Diaria, "10:00").TocaAhora(LunesALasNueve).Should().BeFalse();
    }

    [Fact]
    public void La_semanal_toca_su_dia()
    {
        // 1 es lunes, según ISO. El 7 de septiembre de 2026 es lunes.
        Crear(FrecuenciaDeInforme.Semanal, "08:00", dia: 1).TocaAhora(LunesALasNueve).Should().BeTrue();
    }

    [Fact]
    public void La_semanal_no_toca_otro_dia()
    {
        Crear(FrecuenciaDeInforme.Semanal, "08:00", dia: 3).TocaAhora(LunesALasNueve).Should().BeFalse();
    }

    /// <summary>
    /// El domingo es el 7, no el 0.
    ///
    /// En .NET, <c>DayOfWeek.Sunday</c> vale 0. Guardarlo tal cual haría que «día 1» significara
    /// lunes para quien lo configura y domingo para el planificador, y el informe llegaría un día
    /// tarde toda la vida.
    /// </summary>
    [Fact]
    public void El_domingo_es_el_dia_siete()
    {
        var domingo = new DateTime(2026, 9, 13, 9, 0, 0);
        domingo.DayOfWeek.Should().Be(DayOfWeek.Sunday);

        Crear(FrecuenciaDeInforme.Semanal, "08:00", dia: 7).TocaAhora(domingo).Should().BeTrue();
        Crear(FrecuenciaDeInforme.Semanal, "08:00", dia: 1).TocaAhora(domingo).Should().BeFalse();
    }

    [Fact]
    public void La_mensual_toca_su_dia_del_mes()
    {
        Crear(FrecuenciaDeInforme.Mensual, "08:00", dia: 7).TocaAhora(LunesALasNueve).Should().BeTrue();
        Crear(FrecuenciaDeInforme.Mensual, "08:00", dia: 8).TocaAhora(LunesALasNueve).Should().BeFalse();
    }

    /// <summary>
    /// Lo que impide que un informe diario llegue doce veces.
    ///
    /// El planificador corre cada cinco minutos; sin esta marca, cada vuelta del día volvería a
    /// disparar el mismo informe.
    /// </summary>
    [Fact]
    public void No_toca_dos_veces_el_mismo_dia()
    {
        var programacion = Crear(FrecuenciaDeInforme.Diaria, "08:00");

        programacion.TocaAhora(LunesALasNueve).Should().BeTrue();

        programacion.AnotarGenerada(DateOnly.FromDateTime(LunesALasNueve));

        programacion.TocaAhora(LunesALasNueve).Should().BeFalse();
        programacion.TocaAhora(LunesALasNueve.AddHours(5)).Should().BeFalse();
    }

    [Fact]
    public void Al_dia_siguiente_vuelve_a_tocar()
    {
        var programacion = Crear(FrecuenciaDeInforme.Diaria, "08:00");
        programacion.AnotarGenerada(DateOnly.FromDateTime(LunesALasNueve));

        programacion.TocaAhora(LunesALasNueve.AddDays(1)).Should().BeTrue();
    }

    [Fact]
    public void Una_desactivada_no_toca_nunca()
    {
        var programacion = Crear(FrecuenciaDeInforme.Diaria, "08:00");
        programacion.Desactivar();

        programacion.TocaAhora(LunesALasNueve).Should().BeFalse();
    }

    /// <summary>
    /// Desactivar no borra la marca del día, y por eso reactivar no duplica.
    ///
    /// Es la razón de que desactivar y borrar sean cosas distintas: borrar la programación y
    /// volver a crearla el mismo día sí generaría el informe dos veces.
    /// </summary>
    [Fact]
    public void Reactivar_el_mismo_dia_no_vuelve_a_disparar()
    {
        var programacion = Crear(FrecuenciaDeInforme.Diaria, "08:00");
        programacion.AnotarGenerada(DateOnly.FromDateTime(LunesALasNueve));

        programacion.Desactivar();
        programacion.Activar();

        programacion.TocaAhora(LunesALasNueve).Should().BeFalse();
    }

    #endregion

    #region Lo que no se admite

    [Fact]
    public void Una_semanal_sin_dia_se_rechaza()
    {
        var resultado = ProgramacionDeInforme.Crear(
            Inquilino, Informe, Persona, FrecuenciaDeInforme.Semanal, ReportFormat.Pdf,
            new TimeOnly(8, 0), dia: null);

        resultado.Error.Should().Be(ProgramacionDeInforme.Reglas.FaltaElDia);
    }

    [Fact]
    public void Un_dia_de_semana_fuera_de_rango_se_rechaza()
    {
        ProgramacionDeInforme.Crear(
                Inquilino, Informe, Persona, FrecuenciaDeInforme.Semanal, ReportFormat.Pdf,
                new TimeOnly(8, 0), dia: 8)
            .Error.Should().Be(ProgramacionDeInforme.Reglas.DiaDeSemanaFuera);
    }

    /// <summary>
    /// El día 31 no se admite, y el mensaje dice por qué.
    ///
    /// Un informe programado el 31 no se generaría en febrero ni en los meses de treinta días:
    /// cuatro meses al año fallando en silencio. Es mejor no ofrecerlo.
    /// </summary>
    [Fact]
    public void Un_dia_del_mes_por_encima_de_28_se_rechaza()
    {
        ProgramacionDeInforme.Crear(
                Inquilino, Informe, Persona, FrecuenciaDeInforme.Mensual, ReportFormat.Pdf,
                new TimeOnly(8, 0), dia: 31)
            .Error.Should().Contain("febrero");
    }

    /// <summary>La diaria ignora el día en vez de rechazarlo: no molesta y no significa nada.</summary>
    [Fact]
    public void La_diaria_ignora_el_dia_que_le_pasen()
    {
        var programacion = Crear(FrecuenciaDeInforme.Diaria, "08:00", dia: 5);

        programacion.Dia.Should().BeNull();
        programacion.TocaAhora(LunesALasNueve).Should().BeTrue();
    }

    #endregion
}
