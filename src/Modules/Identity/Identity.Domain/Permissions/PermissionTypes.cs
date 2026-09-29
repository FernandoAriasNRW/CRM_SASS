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
    /// Etiquetas. Con <c>Full</c> sobre todas (<c>Guid.Empty</c>) una persona puede editar y borrar
    /// cualquier etiqueta, no sólo las suyas (ver <c>Tags.Application.Authorization.TagAccess</c>).
    /// </summary>
    public const string Tag = "Tag";

    /// <summary>
    /// El nombre en el vocabulario de la tabla, acepte lo que acepte.
    ///
    /// Se normaliza al guardar y al consultar, no sólo en la migración: un cliente antiguo, una
    /// integración o una pestaña abierta desde antes del cambio pueden seguir mandando el plural,
    /// y con eso volvería a nacer una fila que no consulta nadie.
    /// </summary>
    public static string Normalize(string type) => type switch
    {
        "Tasks" => Task,
        "Projects" => Project,
        "Tickets" => Ticket,
        "Docs" or "Documents" => Document,
        "Webhooks" => Webhook,
        "Teams" => Team,
        "Reports" => Report,
        "Tags" => Tag,
        _ => type
    };

    /// <summary>
    /// El tipo de permiso de una entidad de <see cref="BuildingBlocks.Domain.EntityTypes"/>.
    ///
    /// Desde que los valores de <c>EntityTypes</c> pasaron a inglés («Tarea» → «Task») los dos
    /// vocabularios coinciden y esto es la identidad. Se mantiene como el único punto de paso: si
    /// algún día un tipo de entidad y su permiso se llamaran distinto, se traduce aquí y no en
    /// cada llamador.
    /// </summary>
    public static string FromEntityType(string entityType) => entityType;
}
