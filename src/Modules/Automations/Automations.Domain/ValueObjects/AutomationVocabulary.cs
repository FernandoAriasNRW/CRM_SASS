namespace Automations.Domain.ValueObjects;

/// <summary>
/// Qué puede disparar una regla.
///
/// La lista es cerrada a propósito: cada disparador tiene que traducirse a un evento de dominio
/// que algún módulo emita de verdad. Ofrecer disparadores que no están conectados sería dejar
/// que alguien configure una automatización que nunca se ejecuta y no sepa por qué.
/// </summary>
public static class TriggerTypes
{
    public const string TaskCreated = "TaskCreated";
    public const string TaskStatusChanged = "TaskStatusChanged";
    public const string TaskPriorityChanged = "TaskPriorityChanged";

    /// <summary>
    /// Se revisa una vez al día: para cada tarea sin terminar, cuántos días faltan para su
    /// vencimiento.
    ///
    /// Es el disparador que faltaba y el que más se usa en un producto de este tipo: casi toda
    /// automatización real es «esto va a llegar tarde, que alguien se entere». Los otros tres
    /// reaccionan a algo que alguien hizo; éste reacciona a que **no** ha pasado nada, que es
    /// justo lo que no se nota solo.
    ///
    /// A diferencia de los demás no lo dispara un evento sino un trabajo en segundo plano, y eso
    /// trae un problema que los otros no tienen: volvería a saltar cada día sobre la misma
    /// tarea. Lo resuelve el registro de ejecuciones, que le hace de memoria.
    /// </summary>
    public const string TaskDueSoon = "TaskDueSoon";

    public static IReadOnlyList<string> All() =>
        [TaskCreated, TaskStatusChanged, TaskPriorityChanged, TaskDueSoon];

    public static bool Exists(string type) => All().Contains(type);

    /// <summary>
    /// Los que revisa el trabajo diario en vez de un evento. Sólo éstos necesitan protegerse de
    /// repetirse, porque son los únicos que se evalúan una y otra vez sobre la misma tarea.
    /// </summary>
    public static bool IsTimeBased(string type) => type == TaskDueSoon;
}

/// <summary>
/// Los datos que una condición puede mirar. Se nombran igual en todos los disparadores que los
/// tengan, para que cambiar el disparador de una regla no obligue a reescribir sus condiciones.
/// Qué disparador trae cuáles está en <see cref="ByTrigger"/>.
/// </summary>
public static class EventFields
{
    public const string Status = "Status";
    public const string PreviousStatus = "PreviousStatus";
    public const string Priority = "Priority";
    public const string PreviousPriority = "PreviousPriority";
    public const string ProjectId = "ProjectId";
    public const string AssigneeId = "AssigneeId";

    /// <summary>
    /// Cuántos días faltan para el vencimiento. Negativo si ya venció, 0 si vence hoy.
    ///
    /// Es el primer campo numérico, y lo trae sólo el disparador por tiempo. Se expresa en días
    /// y no como fecha absoluta a propósito: quien escribe la regla piensa en «avísame dos días
    /// antes», no en «el 14 de marzo».
    /// </summary>
    public const string DaysUntilDue = "DaysUntilDue";

    /// <summary>El título. Útil con «Contiene» para reglas por convención de nombre.</summary>
    public const string Title = "Title";

    public static IReadOnlyList<string> All() =>
        [Status, PreviousStatus, Priority, PreviousPriority, ProjectId, AssigneeId,
         DaysUntilDue, Title];

    public static bool Exists(string field) => All().Contains(field);

    /// <summary>
    /// Qué campos trae cada disparador. Es la fuente única: el puente de eventos y el vigilante de
    /// vencimientos rellenan exactamente éstos, el vocabulario los sirve para que la interfaz sólo
    /// ofrezca los del disparador elegido, y la regla rechaza condiciones sobre los demás.
    ///
    /// Antes se ofrecían todos para cualquier disparador, y una condición sobre un campo que el
    /// evento no trae no da ningún error: se evalúa contra nada, no se cumple nunca y la ejecución
    /// queda anotada como «condiciones no cumplidas», que parece un resultado legítimo. Así pasó
    /// con «se crea una tarea» y «el título contiene…».
    ///
    /// Añadir un campo a un disparador es tocar esta tabla **y** quien lo rellena; la prueba que
    /// compara los dos impide que vuelvan a separarse.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ByTrigger =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [TriggerTypes.TaskCreated] = [ProjectId, AssigneeId],
            [TriggerTypes.TaskStatusChanged] = [Status, PreviousStatus, ProjectId],
            [TriggerTypes.TaskPriorityChanged] = [Priority, PreviousPriority, ProjectId],
            [TriggerTypes.TaskDueSoon] = [DaysUntilDue, Status, Priority, ProjectId, AssigneeId, Title],
        };

    /// <summary>Los campos que trae un disparador, o ninguno si el disparador no existe.</summary>
    public static IReadOnlyList<string> ForTrigger(string trigger) =>
        ByTrigger.TryGetValue(trigger, out var fields) ? fields : [];

    public static bool IsCarriedBy(string trigger, string field) => ForTrigger(trigger).Contains(field);

    /// <summary>
    /// Los que se comparan como número y no como texto.
    ///
    /// Importa más de lo que parece: sobre texto, «-1» es mayor que «10» porque se compara
    /// carácter a carácter, y una regla de «lleva más de diez días de retraso» no saltaría nunca
    /// sin dar ningún error.
    /// </summary>
    public static bool IsNumeric(string field) => field == DaysUntilDue;
}

/// <summary>
/// Cómo se compara.
///
/// Aquí decía que no había comparaciones numéricas «porque todos los campos que hoy expone un
/// evento son identificadores o etiquetas», y que se añadirían cuando hubiera un campo numérico.
/// Ya lo hay: <see cref="EventFields.DaysUntilDue"/>.
///
/// **Sólo comparan como número los campos numéricos.** Sobre el resto se rechaza al guardar la
/// regla, en lugar de caer en la comparación alfabética que era lo que había que evitar.
/// </summary>
public static class ConditionOperators
{
    public const string EqualTo = "EqualTo";
    public const string NotEqualTo = "NotEqualTo";
    public const string Contains = "Contains";
    public const string IsEmpty = "IsEmpty";
    public const string LessOrEqual = "LessOrEqual";
    public const string GreaterOrEqual = "GreaterOrEqual";

    public static IReadOnlyList<string> All() =>
        [EqualTo, NotEqualTo, Contains, IsEmpty, LessOrEqual, GreaterOrEqual];

    public static bool Exists(string op) => All().Contains(op);

    /// <summary>El único que no necesita valor de comparación.</summary>
    public static bool NeedsValue(string op) => op != IsEmpty;

    /// <summary>Los que exigen un campo numérico enfrente.</summary>
    public static bool IsNumeric(string op) => op is LessOrEqual or GreaterOrEqual;
}

/// <summary>
/// Qué puede hacer una regla.
///
/// **Todas las acciones son sobre la propia tarea que disparó la regla.** Actuar sobre otras
/// entidades exigiría decir cuáles, y eso es un lenguaje de selección entero. Mandar correos o
/// llamar a webhooks tampoco entra: para eso ya está el módulo de webhooks, y duplicarlo aquí
/// daría dos sitios donde configurar lo mismo.
/// </summary>
public static class ActionTypes
{
    public const string ChangeStatus = "ChangeStatus";
    public const string ChangePriority = "ChangePriority";
    public const string AssignTo = "AssignTo";

    /// <summary>
    /// Avisar a una persona. El valor es su identificador, o
    /// <see cref="AssigneeRecipient"/> para quien tenga la tarea asignada.
    ///
    /// Es la acción que faltaba, y la que hace útiles a las demás: hasta ahora una regla podía
    /// cambiar datos pero no contárselo a nadie, y buena parte de lo que se automatiza en un
    /// producto de este tipo es precisamente avisar.
    ///
    /// **Respeta las preferencias de quien recibe.** Quien apagó un tipo de aviso no empieza a
    /// recibirlo porque alguien configure una automatización: automatizar no es un permiso para
    /// saltarse lo que la persona ya decidió.
    /// </summary>
    public const string Notify = "Notify";

    /// <summary>
    /// Valor especial de <see cref="Notify"/>: avisa a quien tenga la tarea.
    ///
    /// Sin esto habría que escribir el identificador de una persona concreta en la regla, y la
    /// regla dejaría de valer en cuanto la tarea cambiara de manos — que es justo cuando más
    /// falta hace el aviso.
    /// </summary>
    public const string AssigneeRecipient = "Assignee";

    public static IReadOnlyList<string> All() =>
        [ChangeStatus, ChangePriority, AssignTo, Notify];

    public static bool Exists(string type) => All().Contains(type);
}
