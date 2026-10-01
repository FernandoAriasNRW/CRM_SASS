using Automations.Domain.Events;
using Automations.Domain.ValueObjects;
using BuildingBlocks.Domain.Primitives;

namespace Automations.Domain.Entities;

/// <summary>Una condición: qué campo del evento se mira, cómo se compara y contra qué.</summary>
public sealed class AutomationCondition
{
    public Guid Id { get; private set; }
    public string Field { get; private set; } = string.Empty;
    public string Operator { get; private set; } = string.Empty;
    public string? Value { get; private set; }

    private AutomationCondition() { }

    public AutomationCondition(string field, string op, string? value)
    {
        if (!EventFields.Exists(field))
            throw new InvalidOperationException(AutomationRule.Rules.UnknownField);

        if (!ValueObjects.ConditionOperators.Exists(op))
            throw new InvalidOperationException(AutomationRule.Rules.UnknownOperator);

        if (ValueObjects.ConditionOperators.NeedsValue(op) && string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(AutomationRule.Rules.ConditionWithoutValue);

        // «Menor o igual» sobre un campo de texto se rechaza aquí en vez de comparar
        // alfabéticamente. Comparar «Alta» con «Baja» carácter a carácter da una respuesta —y
        // por tanto una regla que salta o no salta— que nadie ha pedido y que no da ningún
        // error. Es preferible no dejar guardarla.
        if (ValueObjects.ConditionOperators.IsNumeric(op) && !EventFields.IsNumeric(field))
            throw new InvalidOperationException(AutomationRule.Rules.NumericOperatorOnText);

        // Y al revés: el valor con el que se compara un campo numérico tiene que ser un número.
        if (EventFields.IsNumeric(field)
            && ValueObjects.ConditionOperators.NeedsValue(op)
            && !int.TryParse(value, out _))
            throw new InvalidOperationException(AutomationRule.Rules.NonNumericValue);

        Id = Guid.NewGuid();
        Field = field;
        Operator = op;
        Value = value?.Trim();
    }
}

/// <summary>Una acción: qué se le hace a la tarea que disparó la regla.</summary>
public sealed class AutomationAction
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;

    private AutomationAction() { }

    public AutomationAction(string type, string value)
    {
        if (!ActionTypes.Exists(type))
            throw new InvalidOperationException(AutomationRule.Rules.UnknownAction);

        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(AutomationRule.Rules.ActionWithoutValue);

        Id = Guid.NewGuid();
        Type = type;
        Value = value.Trim();
    }
}

/// <summary>
/// Una regla de automatización: cuando pasa X, si se cumple Y, haz Z.
///
/// **Las acciones de una regla no disparan otras reglas.** Es la decisión de fondo de este
/// módulo. Encadenarlas exige detectar ciclos —una regla que pone «En progreso» y otra que al
/// verlo lo devuelve a «Por hacer» se llamarían para siempre— y un presupuesto de profundidad, y
/// eso es un proyecto en sí mismo. Prometerlo a medias sería peor: la cascada funcionaría casi
/// siempre y un día se comería la base de datos. Sin cadenas, lo que se configura es lo que pasa.
/// </summary>
public sealed class AutomationRule : AggregateRoot, ITenantEntity
{
    public const int MaxNameLength = 100;
    public const int MaxConditions = 10;
    public const int MaxActions = 5;

    public Guid TenantId { get; private set; }

    /// <summary>Lo que se lee en la lista. Es la única pista de para qué existe la regla.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Uno de <see cref="TriggerTypes"/>.</summary>
    public string Trigger { get; private set; } = string.Empty;

    /// <summary>
    /// Una regla desactivada se conserva pero no se ejecuta. Es lo que permite apagar una
    /// automatización que está haciendo daño sin perder cómo estaba configurada.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    private readonly List<AutomationCondition> _conditions = [];
    public IReadOnlyCollection<AutomationCondition> Conditions => _conditions.AsReadOnly();

    private readonly List<AutomationAction> _actions = [];
    public IReadOnlyCollection<AutomationAction> Actions => _actions.AsReadOnly();

    /// <summary>
    /// Cuántas veces se ha ejecutado. Es lo primero que se mira cuando alguien dice «esta
    /// automatización no funciona»: separa «no salta» de «salta y hace otra cosa».
    /// </summary>
    public int ExecutionCount { get; private set; }

    public DateTime? LastExecutedAtUtc { get; private set; }

    private AutomationRule() { }

    public static AutomationRule Create(
        Guid tenantId,
        string name,
        string trigger,
        IEnumerable<AutomationCondition>? conditions,
        IEnumerable<AutomationAction> actions)
    {
        var rule = new AutomationRule { Id = Guid.NewGuid(), TenantId = tenantId };

        rule.Configure(name, trigger, conditions, actions);
        rule.RaiseDomainEvent(new AutomationRuleDefinedEvent(rule.Id, tenantId, rule.Name, rule.Trigger));

        return rule;
    }

    public void Update(
        string name,
        string trigger,
        IEnumerable<AutomationCondition>? conditions,
        IEnumerable<AutomationAction> actions)
    {
        Configure(name, trigger, conditions, actions);
        RaiseDomainEvent(new AutomationRuleUpdatedEvent(Id, TenantId, Name));
    }

    private void Configure(
        string name,
        string trigger,
        IEnumerable<AutomationCondition>? conditions,
        IEnumerable<AutomationAction> actions)
    {
        var cleanName = (name ?? string.Empty).Trim();

        if (cleanName.Length == 0)
            throw new InvalidOperationException(Rules.NameRequired);

        if (cleanName.Length > MaxNameLength)
            throw new InvalidOperationException(Rules.NameTooLong);

        if (!TriggerTypes.Exists(trigger))
            throw new InvalidOperationException(Rules.UnknownTrigger);

        var actionList = (actions ?? []).ToList();

        // Una regla sin acciones se ejecutaría entera para no hacer nada. Es un error de
        // configuración silencioso, así que no se admite.
        if (actionList.Count == 0)
            throw new InvalidOperationException(Rules.MissingActions);

        if (actionList.Count > MaxActions)
            throw new InvalidOperationException(Rules.TooManyActions);

        var conditionList = (conditions ?? []).ToList();

        if (conditionList.Count > MaxConditions)
            throw new InvalidOperationException(Rules.TooManyConditions);

        Name = cleanName;
        Trigger = trigger;

        _conditions.Clear();
        _conditions.AddRange(conditionList);

        _actions.Clear();
        _actions.AddRange(actionList);
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    /// <summary>Deja constancia de una ejecución. Lo llama el motor, no la interfaz.</summary>
    public void RecordExecution(DateTime atUtc)
    {
        ExecutionCount++;
        LastExecutedAtUtc = atUtc;
    }

    public static class Rules
    {
        public const string NameRequired = "La automatización necesita un nombre";
        public static readonly string NameTooLong =
            $"El nombre no puede pasar de {MaxNameLength} caracteres";
        public const string UnknownTrigger = "Ese disparador no existe";
        public const string UnknownField = "Ese campo no existe en el evento";
        public const string UnknownOperator = "Ese operador no existe";
        public const string ConditionWithoutValue = "La condición necesita un valor con el que comparar";
        public const string UnknownAction = "Esa acción no existe";
        public const string ActionWithoutValue = "La acción necesita un valor";
        public const string MissingActions = "La automatización necesita al menos una acción";
        public static readonly string TooManyActions =
            $"Una automatización no puede tener más de {MaxActions} acciones";
        public static readonly string TooManyConditions =
            $"Una automatización no puede tener más de {MaxConditions} condiciones";
        public const string DuplicateName = "Ya hay una automatización con ese nombre";
        public const string NumericOperatorOnText =
            "«Menor o igual» y «mayor o igual» sólo valen sobre campos numéricos";
        public const string NonNumericValue =
            "Ese campo es numérico, así que hay que compararlo con un número entero";
    }
}
