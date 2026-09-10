using Automations.Domain.Entities;
using Automations.Domain.Servicios;
using Automations.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace UnitTests;

/// <summary>
/// El motor de automatizaciones, probado por su parte que más caro sale equivocar: **cuándo se
/// ejecuta una regla**.
///
/// Equivocarse aquí no da error. Hace que una automatización toque datos que no debía —y quien
/// los ve cambiados no sabe por qué— o que no se ejecute y nadie se entere hasta que alguien
/// pregunta por qué no pasó nada. Como es una función pura se puede recorrer la combinatoria
/// entera sin base de datos.
/// </summary>
public sealed class EvaluadorDeCondicionesTests
{
    private static readonly Dictionary<string, string?> UnCambioDeEstado = new()
    {
        [CampoDelEvento.Estado] = "Done",
        [CampoDelEvento.EstadoAnterior] = "In Progress",
        [CampoDelEvento.ProyectoId] = "11111111-1111-1111-1111-111111111111",
    };

    private static CondicionDeAutomatizacion Condicion(string campo, string operador, string? valor = null)
        => new(campo, operador, valor);

    /// <summary>Sin condiciones, la regla se aplica siempre que salte su disparador.</summary>
    [Fact]
    public void Sin_condiciones_siempre_se_cumple()
    {
        EvaluadorDeCondiciones.Cumple([], UnCambioDeEstado).Should().BeTrue();
    }

    [Fact]
    public void Igual_compara_sin_distinguir_mayusculas()
    {
        var condicion = Condicion(CampoDelEvento.Estado, Operador.Igual, "done");

        EvaluadorDeCondiciones.Cumple([condicion], UnCambioDeEstado).Should().BeTrue();
    }

    [Fact]
    public void Igual_no_se_cumple_con_otro_valor()
    {
        var condicion = Condicion(CampoDelEvento.Estado, Operador.Igual, "To Do");

        EvaluadorDeCondiciones.Cumple([condicion], UnCambioDeEstado).Should().BeFalse();
    }

    [Fact]
    public void Distinto_es_lo_contrario_de_igual()
    {
        EvaluadorDeCondiciones.Cumple(
            [Condicion(CampoDelEvento.Estado, Operador.Distinto, "To Do")], UnCambioDeEstado)
            .Should().BeTrue();

        EvaluadorDeCondiciones.Cumple(
            [Condicion(CampoDelEvento.Estado, Operador.Distinto, "Done")], UnCambioDeEstado)
            .Should().BeFalse();
    }

    [Fact]
    public void Contiene_busca_dentro_del_valor()
    {
        EvaluadorDeCondiciones.Cumple(
            [Condicion(CampoDelEvento.EstadoAnterior, Operador.Contiene, "progress")], UnCambioDeEstado)
            .Should().BeTrue();
    }

    [Fact]
    public void EstaVacio_se_cumple_con_nulo_y_con_espacios()
    {
        var datos = new Dictionary<string, string?> { [CampoDelEvento.ResponsableId] = null };

        EvaluadorDeCondiciones.Cumple(
            [Condicion(CampoDelEvento.ResponsableId, Operador.EstaVacio)], datos)
            .Should().BeTrue();

        EvaluadorDeCondiciones.Cumple(
            [Condicion(CampoDelEvento.ResponsableId, Operador.EstaVacio)],
            new Dictionary<string, string?> { [CampoDelEvento.ResponsableId] = "   " })
            .Should().BeTrue();
    }

    /// <summary>
    /// Un campo que el disparador no trae no es «vacío», es «no aplica». Tratarlo como vacío
    /// haría que una regla escrita para otro disparador se ejecutara por accidente.
    /// </summary>
    [Fact]
    public void Un_campo_que_el_evento_no_trae_no_cuenta_como_vacio()
    {
        EvaluadorDeCondiciones.Cumple(
            [Condicion(CampoDelEvento.Prioridad, Operador.EstaVacio)], UnCambioDeEstado)
            .Should().BeFalse();
    }

    /// <summary>Las condiciones se combinan con Y: quien necesite un «o» crea dos reglas.</summary>
    [Fact]
    public void Todas_las_condiciones_tienen_que_cumplirse()
    {
        var todas = new[]
        {
            Condicion(CampoDelEvento.Estado, Operador.Igual, "Done"),
            Condicion(CampoDelEvento.EstadoAnterior, Operador.Igual, "In Progress"),
        };

        EvaluadorDeCondiciones.Cumple(todas, UnCambioDeEstado).Should().BeTrue();

        var unaFalla = new[]
        {
            Condicion(CampoDelEvento.Estado, Operador.Igual, "Done"),
            Condicion(CampoDelEvento.EstadoAnterior, Operador.Igual, "To Do"),
        };

        EvaluadorDeCondiciones.Cumple(unaFalla, UnCambioDeEstado).Should().BeFalse();
    }
}

/// <summary>Invariantes de la regla de automatización.</summary>
public sealed class AutomationRuleTests
{
    private static AutomationRule NuevaRegla(
        string? nombre = null,
        string? disparador = null,
        IEnumerable<CondicionDeAutomatizacion>? condiciones = null,
        IEnumerable<AccionDeAutomatizacion>? acciones = null)
        => AutomationRule.Create(
            Guid.NewGuid(),
            nombre ?? "Cerrar al revisar",
            disparador ?? TipoDeDisparador.TareaCambiaDeEstado,
            condiciones,
            acciones ?? [new AccionDeAutomatizacion(TipoDeAccion.CambiarPrioridad, "Low")]);

    [Fact]
    public void Una_regla_nace_activa_y_emite_evento()
    {
        var regla = NuevaRegla();

        regla.Activa.Should().BeTrue();
        regla.VecesEjecutada.Should().Be(0);
        regla.DomainEvents.Should().ContainSingle();
    }

    /// <summary>
    /// Una regla sin acciones se ejecutaría entera para no hacer nada: un error de configuración
    /// que no da ninguna señal.
    /// </summary>
    [Fact]
    public void Una_regla_sin_acciones_se_rechaza()
    {
        var accion = () => NuevaRegla(acciones: []);

        accion.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Reglas.SinAcciones);
    }

    [Fact]
    public void Un_disparador_que_no_existe_se_rechaza()
    {
        var accion = () => NuevaRegla(disparador: "CuandoLlueva");

        accion.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Reglas.DisparadorDesconocido);
    }

    [Fact]
    public void Una_regla_sin_nombre_se_rechaza()
    {
        var accion = () => NuevaRegla(nombre: "   ");

        accion.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Un_campo_o_un_operador_que_no_existen_se_rechazan()
    {
        var campoMalo = () => new CondicionDeAutomatizacion("Temperatura", Operador.Igual, "alta");
        campoMalo.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Reglas.CampoDesconocido);

        var operadorMalo = () => new CondicionDeAutomatizacion(CampoDelEvento.Estado, "SeParece", "Done");
        operadorMalo.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Reglas.OperadorDesconocido);
    }

    [Fact]
    public void Una_condicion_que_compara_necesita_con_que_comparar()
    {
        var accion = () => new CondicionDeAutomatizacion(CampoDelEvento.Estado, Operador.Igual, "  ");

        accion.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Reglas.CondicionSinValor);
    }

    /// <summary>«Está vacío» es el único operador que no compara contra nada.</summary>
    [Fact]
    public void EstaVacio_no_necesita_valor()
    {
        var accion = () => new CondicionDeAutomatizacion(CampoDelEvento.ResponsableId, Operador.EstaVacio, null);

        accion.Should().NotThrow();
    }

    [Fact]
    public void Una_accion_que_no_existe_o_sin_valor_se_rechaza()
    {
        var tipoMalo = () => new AccionDeAutomatizacion("MandarUnaPaloma", "sí");
        tipoMalo.Should().Throw<InvalidOperationException>();

        var sinValor = () => new AccionDeAutomatizacion(TipoDeAccion.CambiarEstado, " ");
        sinValor.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Reglas.AccionSinValor);
    }

    /// <summary>
    /// Desactivar conserva la configuración. Es lo que permite apagar una automatización que
    /// está haciendo daño sin perder cómo estaba montada.
    /// </summary>
    [Fact]
    public void Desactivar_y_activar_no_tocan_lo_configurado()
    {
        var regla = NuevaRegla();

        regla.Desactivar();
        regla.Activa.Should().BeFalse();
        regla.Acciones.Should().HaveCount(1);

        regla.Activar();
        regla.Activa.Should().BeTrue();
    }

    [Fact]
    public void Actualizar_reemplaza_condiciones_y_acciones()
    {
        var regla = NuevaRegla();

        regla.Actualizar(
            "Otro nombre", TipoDeDisparador.TareaCreada,
            [new CondicionDeAutomatizacion(CampoDelEvento.ResponsableId, Operador.EstaVacio, null)],
            [new AccionDeAutomatizacion(TipoDeAccion.CambiarEstado, "In Progress")]);

        regla.Nombre.Should().Be("Otro nombre");
        regla.Disparador.Should().Be(TipoDeDisparador.TareaCreada);
        regla.Condiciones.Should().HaveCount(1);
        regla.Acciones.Should().ContainSingle().Which.Tipo.Should().Be(TipoDeAccion.CambiarEstado);
    }

    [Fact]
    public void Anotar_una_ejecucion_deja_rastro()
    {
        var regla = NuevaRegla();
        var cuando = new DateTime(2026, 8, 14, 10, 30, 0, DateTimeKind.Utc);

        regla.AnotarEjecucion(cuando);

        regla.VecesEjecutada.Should().Be(1);
        regla.UltimaEjecucionUtc.Should().Be(cuando);
    }

    [Fact]
    public void No_se_admiten_mas_acciones_de_las_permitidas()
    {
        var demasiadas = Enumerable.Range(0, AutomationRule.MaximoDeAcciones + 1)
            .Select(_ => new AccionDeAutomatizacion(TipoDeAccion.CambiarPrioridad, "Low"));

        var accion = () => NuevaRegla(acciones: demasiadas);

        accion.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Reglas.DemasiadasAcciones);
    }

    #region Comparaciones numéricas y disparador por tiempo

    // Todo esto llega con el disparador de vencimiento, que es el primero que trae un campo
    // numérico. El comentario de Operador decía que las comparaciones numéricas se añadirían
    // «cuando haya un campo numérico»; éstas comprueban que se añadieron bien.

    [Theory]
    [InlineData(Operador.MenorOIgual, "2", "2", true)]
    [InlineData(Operador.MenorOIgual, "1", "2", true)]
    [InlineData(Operador.MenorOIgual, "3", "2", false)]
    [InlineData(Operador.MenorOIgual, "-5", "2", true)]     // vencida hace cinco días
    [InlineData(Operador.MayorOIgual, "10", "10", true)]
    [InlineData(Operador.MayorOIgual, "9", "10", false)]
    public void Los_dias_para_vencer_se_comparan_como_numero(
        string operador, string valorDelEvento, string esperado, bool cumple)
    {
        var condicion = new CondicionDeAutomatizacion(CampoDelEvento.DiasParaVencer, operador, esperado);

        var datos = new Dictionary<string, string?> { [CampoDelEvento.DiasParaVencer] = valorDelEvento };

        EvaluadorDeCondiciones.Cumple([condicion], datos).Should().Be(cumple);
    }

    /// <summary>
    /// La razón de ser de la comparación numérica: sobre texto, «-1» sale mayor que «10» porque
    /// se compara carácter a carácter, y una regla de «lleva más de diez días de retraso» no
    /// saltaría nunca sin dar ningún error.
    /// </summary>
    [Fact]
    public void Una_tarea_muy_vencida_no_cuenta_como_muy_adelantada()
    {
        var condicion = new CondicionDeAutomatizacion(
            CampoDelEvento.DiasParaVencer, Operador.MayorOIgual, "10");

        var datos = new Dictionary<string, string?> { [CampoDelEvento.DiasParaVencer] = "-30" };

        EvaluadorDeCondiciones.Cumple([condicion], datos).Should().BeFalse(
            "faltan menos treinta días, o sea que venció hace un mes: no es «diez o más»");
    }

    [Fact]
    public void Un_operador_numerico_sobre_un_campo_de_texto_no_se_puede_guardar()
    {
        var accion = () => new CondicionDeAutomatizacion(
            CampoDelEvento.Estado, Operador.MenorOIgual, "Done");

        accion.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Reglas.OperadorNumericoSobreTexto);
    }

    [Fact]
    public void Un_campo_numerico_no_se_compara_con_texto()
    {
        var accion = () => new CondicionDeAutomatizacion(
            CampoDelEvento.DiasParaVencer, Operador.Igual, "pronto");

        accion.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Reglas.ValorNoNumerico);
    }

    [Fact]
    public void El_disparador_por_vencimiento_se_reconoce_como_de_tiempo()
    {
        TipoDeDisparador.EsPorTiempo(TipoDeDisparador.TareaPorVencer).Should().BeTrue();

        TipoDeDisparador.EsPorTiempo(TipoDeDisparador.TareaCreada).Should().BeFalse(
            "los de evento saltan una vez porque el evento ocurre una vez; no necesitan memoria");
    }

    [Fact]
    public void Se_puede_configurar_avisar_al_responsable()
    {
        var regla = NuevaRegla(
            disparador: TipoDeDisparador.TareaPorVencer,
            acciones: [new AccionDeAutomatizacion(TipoDeAccion.Notificar, TipoDeAccion.DestinatarioResponsable)]);

        regla.Acciones.Should().ContainSingle()
            .Which.Valor.Should().Be(TipoDeAccion.DestinatarioResponsable);
    }

    #endregion

    #region Registro de ejecuciones

    [Theory]
    [InlineData(ResultadoDeEjecucion.Aplicada)]
    [InlineData(ResultadoDeEjecucion.NoCumplioCondiciones)]
    [InlineData(ResultadoDeEjecucion.Fallida)]
    public void Una_ejecucion_se_anota_con_su_resultado(string resultado)
    {
        var cuando = new DateTime(2026, 8, 14, 10, 30, 0, DateTimeKind.Utc);

        var ejecucion = EjecucionDeAutomatizacion.Anotar(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), resultado, null, cuando);

        ejecucion.Resultado.Should().Be(resultado);
        ejecucion.CuandoUtc.Should().Be(cuando);
        ejecucion.Dia.Should().Be(new DateOnly(2026, 8, 14),
            "el día se guarda aparte para poder preguntar «¿ya se ejecutó hoy?» con una igualdad");
    }

    [Fact]
    public void Un_resultado_inventado_no_se_admite()
    {
        var accion = () => EjecucionDeAutomatizacion.Anotar(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "MasOMenos", null, DateTime.UtcNow);

        accion.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Un mensaje de error puede traer un volcado entero. Se recorta para que una regla mal
    /// configurada no llene la tabla con la misma pila de llamadas repetida cada hora.
    /// </summary>
    [Fact]
    public void El_detalle_se_recorta_en_vez_de_crecer_sin_limite()
    {
        var larguisimo = new string('x', EjecucionDeAutomatizacion.LargoMaximoDelDetalle + 500);

        var ejecucion = EjecucionDeAutomatizacion.Anotar(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            ResultadoDeEjecucion.Fallida, larguisimo, DateTime.UtcNow);

        ejecucion.Detalle!.Length.Should().Be(EjecucionDeAutomatizacion.LargoMaximoDelDetalle);
    }

    #endregion
}
