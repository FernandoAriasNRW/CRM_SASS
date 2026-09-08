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
}
