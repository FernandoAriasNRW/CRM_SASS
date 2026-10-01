using Automations.Domain.Entities;
using Automations.Domain.Services;
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
        [EventFields.Status] = "Done",
        [EventFields.PreviousStatus] = "In Progress",
        [EventFields.ProjectId] = "11111111-1111-1111-1111-111111111111",
    };

    private static AutomationCondition Condicion(string campo, string operador, string? valor = null)
        => new(campo, operador, valor);

    /// <summary>Sin condiciones, la regla se aplica siempre que salte su disparador.</summary>
    [Fact]
    public void Sin_condiciones_siempre_se_cumple()
    {
        ConditionEvaluator.Matches([], UnCambioDeEstado).Should().BeTrue();
    }

    [Fact]
    public void Igual_compara_sin_distinguir_mayusculas()
    {
        var condicion = Condicion(EventFields.Status, ConditionOperators.EqualTo, "done");

        ConditionEvaluator.Matches([condicion], UnCambioDeEstado).Should().BeTrue();
    }

    [Fact]
    public void Igual_no_se_cumple_con_otro_valor()
    {
        var condicion = Condicion(EventFields.Status, ConditionOperators.EqualTo, "To Do");

        ConditionEvaluator.Matches([condicion], UnCambioDeEstado).Should().BeFalse();
    }

    [Fact]
    public void Distinto_es_lo_contrario_de_igual()
    {
        ConditionEvaluator.Matches(
            [Condicion(EventFields.Status, ConditionOperators.NotEqualTo, "To Do")], UnCambioDeEstado)
            .Should().BeTrue();

        ConditionEvaluator.Matches(
            [Condicion(EventFields.Status, ConditionOperators.NotEqualTo, "Done")], UnCambioDeEstado)
            .Should().BeFalse();
    }

    [Fact]
    public void Contiene_busca_dentro_del_valor()
    {
        ConditionEvaluator.Matches(
            [Condicion(EventFields.PreviousStatus, ConditionOperators.Contains, "progress")], UnCambioDeEstado)
            .Should().BeTrue();
    }

    [Fact]
    public void EstaVacio_se_cumple_con_nulo_y_con_espacios()
    {
        var datos = new Dictionary<string, string?> { [EventFields.AssigneeId] = null };

        ConditionEvaluator.Matches(
            [Condicion(EventFields.AssigneeId, ConditionOperators.IsEmpty)], datos)
            .Should().BeTrue();

        ConditionEvaluator.Matches(
            [Condicion(EventFields.AssigneeId, ConditionOperators.IsEmpty)],
            new Dictionary<string, string?> { [EventFields.AssigneeId] = "   " })
            .Should().BeTrue();
    }

    /// <summary>
    /// Un campo que el disparador no trae no es «vacío», es «no aplica». Tratarlo como vacío
    /// haría que una regla escrita para otro disparador se ejecutara por accidente.
    /// </summary>
    [Fact]
    public void Un_campo_que_el_evento_no_trae_no_cuenta_como_vacio()
    {
        ConditionEvaluator.Matches(
            [Condicion(EventFields.Priority, ConditionOperators.IsEmpty)], UnCambioDeEstado)
            .Should().BeFalse();
    }

    /// <summary>Las condiciones se combinan con Y: quien necesite un «o» crea dos reglas.</summary>
    [Fact]
    public void Todas_las_condiciones_tienen_que_cumplirse()
    {
        var todas = new[]
        {
            Condicion(EventFields.Status, ConditionOperators.EqualTo, "Done"),
            Condicion(EventFields.PreviousStatus, ConditionOperators.EqualTo, "In Progress"),
        };

        ConditionEvaluator.Matches(todas, UnCambioDeEstado).Should().BeTrue();

        var unaFalla = new[]
        {
            Condicion(EventFields.Status, ConditionOperators.EqualTo, "Done"),
            Condicion(EventFields.PreviousStatus, ConditionOperators.EqualTo, "To Do"),
        };

        ConditionEvaluator.Matches(unaFalla, UnCambioDeEstado).Should().BeFalse();
    }
}

/// <summary>Invariantes de la regla de automatización.</summary>
public sealed class AutomationRuleTests
{
    private static AutomationRule NuevaRegla(
        string? nombre = null,
        string? disparador = null,
        IEnumerable<AutomationCondition>? condiciones = null,
        IEnumerable<AutomationAction>? acciones = null)
        => AutomationRule.Create(
            Guid.NewGuid(),
            nombre ?? "Cerrar al revisar",
            disparador ?? TriggerTypes.TaskStatusChanged,
            condiciones,
            acciones ?? [new AutomationAction(ActionTypes.ChangePriority, "Low")]);

    [Fact]
    public void Una_regla_nace_activa_y_emite_evento()
    {
        var regla = NuevaRegla();

        regla.IsActive.Should().BeTrue();
        regla.ExecutionCount.Should().Be(0);
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
            .WithMessage(AutomationRule.Rules.MissingActions);
    }

    [Fact]
    public void Un_disparador_que_no_existe_se_rechaza()
    {
        var accion = () => NuevaRegla(disparador: "CuandoLlueva");

        accion.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Rules.UnknownTrigger);
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
        var campoMalo = () => new AutomationCondition("Temperatura", ConditionOperators.EqualTo, "alta");
        campoMalo.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Rules.UnknownField);

        var operadorMalo = () => new AutomationCondition(EventFields.Status, "SeParece", "Done");
        operadorMalo.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Rules.UnknownOperator);
    }

    [Fact]
    public void Una_condicion_que_compara_necesita_con_que_comparar()
    {
        var accion = () => new AutomationCondition(EventFields.Status, ConditionOperators.EqualTo, "  ");

        accion.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Rules.ConditionWithoutValue);
    }

    /// <summary>«Está vacío» es el único operador que no compara contra nada.</summary>
    [Fact]
    public void EstaVacio_no_necesita_valor()
    {
        var accion = () => new AutomationCondition(EventFields.AssigneeId, ConditionOperators.IsEmpty, null);

        accion.Should().NotThrow();
    }

    [Fact]
    public void Una_accion_que_no_existe_o_sin_valor_se_rechaza()
    {
        var tipoMalo = () => new AutomationAction("MandarUnaPaloma", "sí");
        tipoMalo.Should().Throw<InvalidOperationException>();

        var sinValor = () => new AutomationAction(ActionTypes.ChangeStatus, " ");
        sinValor.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Rules.ActionWithoutValue);
    }

    /// <summary>
    /// Desactivar conserva la configuración. Es lo que permite apagar una automatización que
    /// está haciendo daño sin perder cómo estaba montada.
    /// </summary>
    [Fact]
    public void Desactivar_y_activar_no_tocan_lo_configurado()
    {
        var regla = NuevaRegla();

        regla.Deactivate();
        regla.IsActive.Should().BeFalse();
        regla.Actions.Should().HaveCount(1);

        regla.Activate();
        regla.IsActive.Should().BeTrue();
    }

    /// <summary>
    /// La tabla de campos por disparador cubre todos los disparadores y sólo nombra campos que
    /// existen. Un disparador sin entrada no admitiría ninguna condición, y nadie sabría por qué.
    /// </summary>
    [Fact]
    public void Every_trigger_declares_which_existing_fields_it_carries()
    {
        EventFields.ByTrigger.Keys.Should().BeEquivalentTo(TriggerTypes.All());

        foreach (var (trigger, fields) in EventFields.ByTrigger)
        {
            fields.Should().NotBeEmpty(trigger);
            fields.Should().OnlyContain(f => EventFields.Exists(f), trigger);
            fields.Should().OnlyHaveUniqueItems(trigger);
        }
    }

    /// <summary>
    /// El caso medido: «se crea una tarea» no trae el título, así que «el título contiene 8b» se
    /// anotaba como condiciones no cumplidas sin avisar a nadie. Ahora no se deja guardar.
    /// </summary>
    [Fact]
    public void A_condition_on_a_field_the_trigger_does_not_carry_is_rejected()
    {
        var accion = () => NuevaRegla(
            disparador: TriggerTypes.TaskCreated,
            condiciones: [new AutomationCondition(EventFields.Title, ConditionOperators.Contains, "8b")]);

        accion.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Rules.FieldNotInTrigger);
    }

    [Fact]
    public void The_same_condition_is_accepted_on_a_trigger_that_carries_the_field()
    {
        var regla = NuevaRegla(
            disparador: TriggerTypes.TaskDueSoon,
            condiciones: [new AutomationCondition(EventFields.Title, ConditionOperators.Contains, "8b")]);

        regla.Conditions.Should().ContainSingle().Which.Field.Should().Be(EventFields.Title);
    }

    /// <summary>Cambiar el disparador al editar también se comprueba, no sólo al crear.</summary>
    [Fact]
    public void Changing_the_trigger_to_one_without_the_field_is_rejected()
    {
        var regla = NuevaRegla(
            disparador: TriggerTypes.TaskStatusChanged,
            condiciones: [new AutomationCondition(EventFields.Status, ConditionOperators.EqualTo, "Done")]);

        var accion = () => regla.Update(
            regla.Name, TriggerTypes.TaskCreated,
            [new AutomationCondition(EventFields.Status, ConditionOperators.EqualTo, "Done")],
            [new AutomationAction(ActionTypes.ChangePriority, "Low")]);

        accion.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Rules.FieldNotInTrigger);
        regla.Trigger.Should().Be(TriggerTypes.TaskStatusChanged);
    }

    [Fact]
    public void Actualizar_reemplaza_condiciones_y_acciones()
    {
        var regla = NuevaRegla();

        regla.Update(
            "Otro nombre", TriggerTypes.TaskCreated,
            [new AutomationCondition(EventFields.AssigneeId, ConditionOperators.IsEmpty, null)],
            [new AutomationAction(ActionTypes.ChangeStatus, "In Progress")]);

        regla.Name.Should().Be("Otro nombre");
        regla.Trigger.Should().Be(TriggerTypes.TaskCreated);
        regla.Conditions.Should().HaveCount(1);
        regla.Actions.Should().ContainSingle().Which.Type.Should().Be(ActionTypes.ChangeStatus);
    }

    [Fact]
    public void Anotar_una_ejecucion_deja_rastro()
    {
        var regla = NuevaRegla();
        var cuando = new DateTime(2026, 8, 14, 10, 30, 0, DateTimeKind.Utc);

        regla.RecordExecution(cuando);

        regla.ExecutionCount.Should().Be(1);
        regla.LastExecutedAtUtc.Should().Be(cuando);
    }

    [Fact]
    public void No_se_admiten_mas_acciones_de_las_permitidas()
    {
        var demasiadas = Enumerable.Range(0, AutomationRule.MaxActions + 1)
            .Select(_ => new AutomationAction(ActionTypes.ChangePriority, "Low"));

        var accion = () => NuevaRegla(acciones: demasiadas);

        accion.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Rules.TooManyActions);
    }

    #region Comparaciones numéricas y disparador por tiempo

    // Todo esto llega con el disparador de vencimiento, que es el primero que trae un campo
    // numérico. El comentario de Operador decía que las comparaciones numéricas se añadirían
    // «cuando haya un campo numérico»; éstas comprueban que se añadieron bien.

    [Theory]
    [InlineData(ConditionOperators.LessOrEqual, "2", "2", true)]
    [InlineData(ConditionOperators.LessOrEqual, "1", "2", true)]
    [InlineData(ConditionOperators.LessOrEqual, "3", "2", false)]
    [InlineData(ConditionOperators.LessOrEqual, "-5", "2", true)]     // vencida hace cinco días
    [InlineData(ConditionOperators.GreaterOrEqual, "10", "10", true)]
    [InlineData(ConditionOperators.GreaterOrEqual, "9", "10", false)]
    public void Los_dias_para_vencer_se_comparan_como_numero(
        string operador, string valorDelEvento, string esperado, bool cumple)
    {
        var condicion = new AutomationCondition(EventFields.DaysUntilDue, operador, esperado);

        var datos = new Dictionary<string, string?> { [EventFields.DaysUntilDue] = valorDelEvento };

        ConditionEvaluator.Matches([condicion], datos).Should().Be(cumple);
    }

    /// <summary>
    /// La razón de ser de la comparación numérica: sobre texto, «-1» sale mayor que «10» porque
    /// se compara carácter a carácter, y una regla de «lleva más de diez días de retraso» no
    /// saltaría nunca sin dar ningún error.
    /// </summary>
    [Fact]
    public void Una_tarea_muy_vencida_no_cuenta_como_muy_adelantada()
    {
        var condicion = new AutomationCondition(
            EventFields.DaysUntilDue, ConditionOperators.GreaterOrEqual, "10");

        var datos = new Dictionary<string, string?> { [EventFields.DaysUntilDue] = "-30" };

        ConditionEvaluator.Matches([condicion], datos).Should().BeFalse(
            "faltan menos treinta días, o sea que venció hace un mes: no es «diez o más»");
    }

    [Fact]
    public void Un_operador_numerico_sobre_un_campo_de_texto_no_se_puede_guardar()
    {
        var accion = () => new AutomationCondition(
            EventFields.Status, ConditionOperators.LessOrEqual, "Done");

        accion.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Rules.NumericOperatorOnText);
    }

    [Fact]
    public void Un_campo_numerico_no_se_compara_con_texto()
    {
        var accion = () => new AutomationCondition(
            EventFields.DaysUntilDue, ConditionOperators.EqualTo, "pronto");

        accion.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Rules.NonNumericValue);
    }

    [Fact]
    public void El_disparador_por_vencimiento_se_reconoce_como_de_tiempo()
    {
        TriggerTypes.IsTimeBased(TriggerTypes.TaskDueSoon).Should().BeTrue();

        TriggerTypes.IsTimeBased(TriggerTypes.TaskCreated).Should().BeFalse(
            "los de evento saltan una vez porque el evento ocurre una vez; no necesitan memoria");
    }

    [Fact]
    public void Se_puede_configurar_avisar_al_responsable()
    {
        var regla = NuevaRegla(
            disparador: TriggerTypes.TaskDueSoon,
            acciones: [new AutomationAction(ActionTypes.Notify, ActionTypes.AssigneeRecipient)]);

        regla.Actions.Should().ContainSingle()
            .Which.Value.Should().Be(ActionTypes.AssigneeRecipient);
    }

    #endregion

    #region Registro de ejecuciones

    [Theory]
    [InlineData(ExecutionOutcomes.Applied)]
    [InlineData(ExecutionOutcomes.ConditionsNotMet)]
    [InlineData(ExecutionOutcomes.Failed)]
    public void Una_ejecucion_se_anota_con_su_resultado(string resultado)
    {
        var cuando = new DateTime(2026, 8, 14, 10, 30, 0, DateTimeKind.Utc);

        var ejecucion = AutomationExecution.Record(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), resultado, null, cuando);

        ejecucion.Outcome.Should().Be(resultado);
        ejecucion.AtUtc.Should().Be(cuando);
        ejecucion.Day.Should().Be(new DateOnly(2026, 8, 14),
            "el día se guarda aparte para poder preguntar «¿ya se ejecutó hoy?» con una igualdad");
    }

    [Fact]
    public void Un_resultado_inventado_no_se_admite()
    {
        var accion = () => AutomationExecution.Record(
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
        var larguisimo = new string('x', AutomationExecution.MaxDetailLength + 500);

        var ejecucion = AutomationExecution.Record(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            ExecutionOutcomes.Failed, larguisimo, DateTime.UtcNow);

        ejecucion.Detail!.Length.Should().Be(AutomationExecution.MaxDetailLength);
    }

    #endregion
}
