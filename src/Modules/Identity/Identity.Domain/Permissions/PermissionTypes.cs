namespace Identity.Domain.Permissions;

/// <summary>
/// Cómo se llama cada cosa en la tabla de permisos. <b>Un solo vocabulario, en singular.</b>
///
/// Había dos. Los permisos por rol que sembraba la aplicación y los que guardaba la pantalla de
/// permisos iban en plural —«Tasks», «Projects», «Docs»—; los comandos, al pedir autorización,
/// preguntan en singular —«Task»—. Nunca casaban, así que <b>ningún permiso por rol llegó a
/// aplicarse</b>: quitarle a los miembros la edición de tareas desde la pantalla no cambiaba nada,
/// y lo que decidía era el atajo del administrador y el permiso por defecto de los miembros.
///
/// Se elige el singular porque es el que ya consultan los comandos y el que escribe la
/// compartición; cambiar los comandos habría sido tocar trece ficheros de otro módulo para lo
/// mismo.
/// </summary>
public static class PermissionTypes
{
    public const string Task = "Task";
    public const string Project = "Project";
    public const string Ticket = "Ticket";
    public const string Document = "Document";
    public const string Webhook = "Webhook";
    public const string Team = "Team";
    public const string Report = "Report";
    public const string Settings = "Settings";

    /// <summary>
    /// El nombre en el vocabulario de la tabla, acepte lo que acepte.
    ///
    /// Se normaliza al guardar y al consultar, no sólo en la migración: un cliente antiguo, una
    /// integración o una pestaña abierta desde antes del cambio pueden seguir mandando el plural,
    /// y con eso volvería a nacer una fila que no consulta nadie.
    /// </summary>
    /// <summary>
    /// El tipo de permiso de una entidad de <see cref="BuildingBlocks.Domain.EntityTypes"/>.
    ///
    /// Vivía en Application como <c>VocabularioDePermisos</c>, separado de <see cref="Normalize"/>:
    /// dos traductores para el mismo vocabulario, en dos capas. Mientras los valores de
    /// <c>EntityTypes</c> sigan en español («Tarea») esto traduce; cuando pasen a inglés, será la
    /// identidad.
    /// </summary>
    public static string FromEntityType(string entityType) => entityType switch
    {
        BuildingBlocks.Domain.EntityTypes.Task => Task,
        BuildingBlocks.Domain.EntityTypes.Project => Project,
        BuildingBlocks.Domain.EntityTypes.Ticket => Ticket,
        BuildingBlocks.Domain.EntityTypes.Document => Document,
        _ => entityType
    };

    public static string Normalize(string type) => type switch
    {
        "Tasks" => Task,
        "Projects" => Project,
        "Tickets" => Ticket,
        "Docs" or "Documents" => Document,
        "Webhooks" => Webhook,
        "Teams" => Team,
        "Reports" => Report,
        _ => type
    };
}
