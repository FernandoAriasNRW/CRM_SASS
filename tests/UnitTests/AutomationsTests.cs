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
public sealed class ConditionEvaluatorTests
{
    private static readonly Dictionary<string, string?> AStatusChange = new()
    {
        [EventFields.Status] = "Done",
        [EventFields.PreviousStatus] = "In Progress",
        [EventFields.ProjectId] = "11111111-1111-1111-1111-111111111111",
    };

    private static AutomationCondition Condition(string field, string op, string? value = null)
        => new(field, op, value);

    /// <summary>Sin condiciones, la regla se aplica siempre que salte su disparador.</summary>
    [Fact]
    public void Without_conditions_it_always_matches()
    {
        ConditionEvaluator.Matches([], AStatusChange).Should().BeTrue();
    }

    [Fact]
    public void EqualTo_ignores_case()
    {
        var condition = Condition(EventFields.Status, ConditionOperators.EqualTo, "done");

        ConditionEvaluator.Matches([condition], AStatusChange).Should().BeTrue();
    }

    [Fact]
    public void EqualTo_does_not_match_another_value()
    {
        var condition = Condition(EventFields.Status, ConditionOperators.EqualTo, "To Do");

        ConditionEvaluator.Matches([condition], AStatusChange).Should().BeFalse();
    }

    [Fact]
    public void NotEqualTo_is_the_opposite_of_EqualTo()
    {
        ConditionEvaluator.Matches(
            [Condition(EventFields.Status, ConditionOperators.NotEqualTo, "To Do")], AStatusChange)
            .Should().BeTrue();

        ConditionEvaluator.Matches(
            [Condition(EventFields.Status, ConditionOperators.NotEqualTo, "Done")], AStatusChange)
            .Should().BeFalse();
    }

    [Fact]
    public void Contains_searches_inside_the_value()
    {
        ConditionEvaluator.Matches(
            [Condition(EventFields.PreviousStatus, ConditionOperators.Contains, "progress")], AStatusChange)
            .Should().BeTrue();
    }

    [Fact]
    public void IsEmpty_matches_null_and_blank()
    {
        var data = new Dictionary<string, string?> { [EventFields.AssigneeId] = null };

        ConditionEvaluator.Matches(
            [Condition(EventFields.AssigneeId, ConditionOperators.IsEmpty)], data)
            .Should().BeTrue();

        ConditionEvaluator.Matches(
            [Condition(EventFields.AssigneeId, ConditionOperators.IsEmpty)],
            new Dictionary<string, string?> { [EventFields.AssigneeId] = "   " })
            .Should().BeTrue();
    }

    /// <summary>
    /// Un campo que el disparador no trae no es «vacío», es «no aplica». Tratarlo como vacío
    /// haría que una regla escrita para otro disparador se ejecutara por accidente.
    /// </summary>
    [Fact]
    public void A_field_missing_from_the_event_does_not_count_as_empty()
    {
        ConditionEvaluator.Matches(
            [Condition(EventFields.Priority, ConditionOperators.IsEmpty)], AStatusChange)
            .Should().BeFalse();
    }

    /// <summary>Las condiciones se combinan con Y: quien necesite un «o» crea dos reglas.</summary>
    [Fact]
    public void All_conditions_must_match()
    {
        var all = new[]
        {
            Condition(EventFields.Status, ConditionOperators.EqualTo, "Done"),
            Condition(EventFields.PreviousStatus, ConditionOperators.EqualTo, "In Progress"),
        };

        ConditionEvaluator.Matches(all, AStatusChange).Should().BeTrue();

        var oneFails = new[]
        {
            Condition(EventFields.Status, ConditionOperators.EqualTo, "Done"),
            Condition(EventFields.PreviousStatus, ConditionOperators.EqualTo, "To Do"),
        };

        ConditionEvaluator.Matches(oneFails, AStatusChange).Should().BeFalse();
    }
}

/// <summary>Invariantes de la regla de automatización.</summary>
public sealed class AutomationRuleTests
{
    private static AutomationRule NewRule(
        string? name = null,
        string? trigger = null,
        IEnumerable<AutomationCondition>? conditions = null,
        IEnumerable<AutomationAction>? actions = null)
        => AutomationRule.Create(
            Guid.NewGuid(),
            name ?? "Cerrar al revisar",
            trigger ?? TriggerTypes.TaskStatusChanged,
            conditions,
            actions ?? [new AutomationAction(ActionTypes.ChangePriority, "Low")]);

    [Fact]
    public void A_rule_starts_active_and_raises_an_event()
    {
        var rule = NewRule();

        rule.IsActive.Should().BeTrue();
        rule.ExecutionCount.Should().Be(0);
        rule.DomainEvents.Should().ContainSingle();
    }

    /// <summary>
    /// Una regla sin acciones se ejecutaría entera para no hacer nada: un error de configuración
    /// que no da ninguna señal.
    /// </summary>
    [Fact]
    public void A_rule_without_actions_is_rejected()
    {
        var act = () => NewRule(actions: []);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Rules.MissingActions);
    }

    [Fact]
    public void An_unknown_trigger_is_rejected()
    {
        var act = () => NewRule(trigger: "CuandoLlueva");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Rules.UnknownTrigger);
    }

    [Fact]
    public void A_rule_without_name_is_rejected()
    {
        var act = () => NewRule(name: "   ");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void An_unknown_field_or_operator_is_rejected()
    {
        var badField = () => new AutomationCondition("Temperatura", ConditionOperators.EqualTo, "alta");
        badField.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Rules.UnknownField);

        var badOperator = () => new AutomationCondition(EventFields.Status, "SeParece", "Done");
        badOperator.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Rules.UnknownOperator);
    }

    [Fact]
    public void A_comparing_condition_needs_something_to_compare_with()
    {
        var act = () => new AutomationCondition(EventFields.Status, ConditionOperators.EqualTo, "  ");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Rules.ConditionWithoutValue);
    }

    /// <summary>«Está vacío» es el único operador que no compara contra nada.</summary>
    [Fact]
    public void IsEmpty_needs_no_value()
    {
        var act = () => new AutomationCondition(EventFields.AssigneeId, ConditionOperators.IsEmpty, null);

        act.Should().NotThrow();
    }

    [Fact]
    public void An_unknown_action_or_one_without_value_is_rejected()
    {
        var badType = () => new AutomationAction("MandarUnaPaloma", "sí");
        badType.Should().Throw<InvalidOperationException>();

        var withoutValue = () => new AutomationAction(ActionTypes.ChangeStatus, " ");
        withoutValue.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Rules.ActionWithoutValue);
    }

    /// <summary>
    /// Desactivar conserva la configuración. Es lo que permite apagar una automatización que
    /// está haciendo daño sin perder cómo estaba montada.
    /// </summary>
    [Fact]
    public void Deactivating_and_activating_leave_the_configuration_alone()
    {
        var rule = NewRule();

        rule.Deactivate();
        rule.IsActive.Should().BeFalse();
        rule.Actions.Should().HaveCount(1);

        rule.Activate();
        rule.IsActive.Should().BeTrue();
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
    /// «Se crea una tarea» no tiene estado anterior: una condición sobre él se anotaría como
    /// condiciones no cumplidas sin avisar a nadie. No se deja guardar.
    /// </summary>
    [Fact]
    public void A_condition_on_a_field_the_trigger_does_not_carry_is_rejected()
    {
        var act = () => NewRule(
            trigger: TriggerTypes.TaskCreated,
            conditions: [new AutomationCondition(EventFields.PreviousStatus, ConditionOperators.EqualTo, "Done")]);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Rules.FieldNotInTrigger);
    }

    [Fact]
    public void The_same_condition_is_accepted_on_a_trigger_that_carries_the_field()
    {
        var rule = NewRule(
            trigger: TriggerTypes.TaskStatusChanged,
            conditions: [new AutomationCondition(EventFields.PreviousStatus, ConditionOperators.EqualTo, "Done")]);

        rule.Conditions.Should().ContainSingle().Which.Field.Should().Be(EventFields.PreviousStatus);
    }

    /// <summary>
    /// Los datos de la tarea —título, estado, prioridad, proyecto y responsable— los traen todos
    /// los disparadores, para que una condición sobre la tarea valga en cualquiera. Es lo que hace
    /// posible el caso medido: «se crea una tarea» y «el título contiene 8b».
    /// </summary>
    [Fact]
    public void Every_trigger_carries_the_task_data()
    {
        string[] taskData =
            [EventFields.Title, EventFields.Status, EventFields.Priority, EventFields.ProjectId, EventFields.AssigneeId];

        foreach (var trigger in TriggerTypes.All())
            EventFields.ForTrigger(trigger).Should().Contain(taskData, trigger);
    }

    /// <summary>Cambiar el disparador al editar también se comprueba, no sólo al crear.</summary>
    [Fact]
    public void Changing_the_trigger_to_one_without_the_field_is_rejected()
    {
        var rule = NewRule(
            trigger: TriggerTypes.TaskStatusChanged,
            conditions: [new AutomationCondition(EventFields.PreviousStatus, ConditionOperators.EqualTo, "Done")]);

        var act = () => rule.Update(
            rule.Name, TriggerTypes.TaskCreated,
            [new AutomationCondition(EventFields.PreviousStatus, ConditionOperators.EqualTo, "Done")],
            [new AutomationAction(ActionTypes.ChangePriority, "Low")]);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Rules.FieldNotInTrigger);
        rule.Trigger.Should().Be(TriggerTypes.TaskStatusChanged);
    }

    [Fact]
    public void Update_replaces_conditions_and_actions()
    {
        var rule = NewRule();

        rule.Update(
            "Otro nombre", TriggerTypes.TaskCreated,
            [new AutomationCondition(EventFields.AssigneeId, ConditionOperators.IsEmpty, null)],
            [new AutomationAction(ActionTypes.ChangeStatus, "In Progress")]);

        rule.Name.Should().Be("Otro nombre");
        rule.Trigger.Should().Be(TriggerTypes.TaskCreated);
        rule.Conditions.Should().HaveCount(1);
        rule.Actions.Should().ContainSingle().Which.Type.Should().Be(ActionTypes.ChangeStatus);
    }

    [Fact]
    public void Recording_an_execution_leaves_a_trace()
    {
        var rule = NewRule();
        var atUtc = new DateTime(2026, 8, 14, 10, 30, 0, DateTimeKind.Utc);

        rule.RecordExecution(atUtc);

        rule.ExecutionCount.Should().Be(1);
        rule.LastExecutedAtUtc.Should().Be(atUtc);
    }

    [Fact]
    public void No_more_actions_than_allowed_are_accepted()
    {
        var tooMany = Enumerable.Range(0, AutomationRule.MaxActions + 1)
            .Select(_ => new AutomationAction(ActionTypes.ChangePriority, "Low"));

        var act = () => NewRule(actions: tooMany);

        act.Should().Throw<InvalidOperationException>()
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
    public void Days_until_due_are_compared_as_numbers(
        string op, string eventValue, string expected, bool matches)
    {
        var condition = new AutomationCondition(EventFields.DaysUntilDue, op, expected);

        var data = new Dictionary<string, string?> { [EventFields.DaysUntilDue] = eventValue };

        ConditionEvaluator.Matches([condition], data).Should().Be(matches);
    }

    /// <summary>
    /// La razón de ser de la comparación numérica: sobre texto, «-1» sale mayor que «10» porque
    /// se compara carácter a carácter, y una regla de «lleva más de diez días de retraso» no
    /// saltaría nunca sin dar ningún error.
    /// </summary>
    [Fact]
    public void A_long_overdue_task_does_not_count_as_far_ahead()
    {
        var condition = new AutomationCondition(
            EventFields.DaysUntilDue, ConditionOperators.GreaterOrEqual, "10");

        var data = new Dictionary<string, string?> { [EventFields.DaysUntilDue] = "-30" };

        ConditionEvaluator.Matches([condition], data).Should().BeFalse(
            "faltan menos treinta días, o sea que venció hace un mes: no es «diez o más»");
    }

    [Fact]
    public void A_numeric_operator_on_a_text_field_cannot_be_saved()
    {
        var act = () => new AutomationCondition(
            EventFields.Status, ConditionOperators.LessOrEqual, "Done");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Rules.NumericOperatorOnText);
    }

    [Fact]
    public void A_numeric_field_is_not_compared_as_text()
    {
        var act = () => new AutomationCondition(
            EventFields.DaysUntilDue, ConditionOperators.EqualTo, "pronto");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage(AutomationRule.Rules.NonNumericValue);
    }

    [Fact]
    public void The_due_date_trigger_is_recognised_as_time_based()
    {
        TriggerTypes.IsTimeBased(TriggerTypes.TaskDueSoon).Should().BeTrue();

        TriggerTypes.IsTimeBased(TriggerTypes.TaskCreated).Should().BeFalse(
            "los de evento saltan una vez porque el evento ocurre una vez; no necesitan memoria");
    }

    [Fact]
    public void Notifying_the_assignee_can_be_configured()
    {
        var rule = NewRule(
            trigger: TriggerTypes.TaskDueSoon,
            actions: [new AutomationAction(ActionTypes.Notify, ActionTypes.AssigneeRecipient)]);

        rule.Actions.Should().ContainSingle()
            .Which.Value.Should().Be(ActionTypes.AssigneeRecipient);
    }

    #endregion

    #region Registro de ejecuciones

    [Theory]
    [InlineData(ExecutionOutcomes.Applied)]
    [InlineData(ExecutionOutcomes.ConditionsNotMet)]
    [InlineData(ExecutionOutcomes.Failed)]
    public void An_execution_is_recorded_with_its_outcome(string result)
    {
        var atUtc = new DateTime(2026, 8, 14, 10, 30, 0, DateTimeKind.Utc);

        var execution = AutomationExecution.Record(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), result, null, atUtc);

        execution.Outcome.Should().Be(result);
        execution.AtUtc.Should().Be(atUtc);
        execution.Day.Should().Be(new DateOnly(2026, 8, 14),
            "el día se guarda aparte para poder preguntar «¿ya se ejecutó hoy?» con una igualdad");
    }

    [Fact]
    public void A_made_up_outcome_is_not_accepted()
    {
        var act = () => AutomationExecution.Record(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "MasOMenos", null, DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Un mensaje de error puede traer un volcado entero. Se recorta para que una regla mal
    /// configurada no llene la tabla con la misma pila de llamadas repetida cada hora.
    /// </summary>
    [Fact]
    public void The_detail_is_trimmed_instead_of_growing_without_limit()
    {
        var larguisimo = new string('x', AutomationExecution.MaxDetailLength + 500);

        var execution = AutomationExecution.Record(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            ExecutionOutcomes.Failed, larguisimo, DateTime.UtcNow);

        execution.Detail!.Length.Should().Be(AutomationExecution.MaxDetailLength);
    }

    #endregion
}
