namespace Identity.Domain.Permisos;

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
public static class TiposDePermiso
{
    public const string Tarea = "Task";
    public const string Proyecto = "Project";
    public const string Ticket = "Ticket";
    public const string Documento = "Document";
    public const string Webhook = "Webhook";
    public const string Equipo = "Team";
    public const string Informe = "Report";
    public const string Configuracion = "Settings";

    /// <summary>
    /// El nombre en el vocabulario de la tabla, acepte lo que acepte.
    ///
    /// Se normaliza al guardar y al consultar, no sólo en la migración: un cliente antiguo, una
    /// integración o una pestaña abierta desde antes del cambio pueden seguir mandando el plural,
    /// y con eso volvería a nacer una fila que no consulta nadie.
    /// </summary>
    public static string Normalizar(string tipo) => tipo switch
    {
        "Tasks" => Tarea,
        "Projects" => Proyecto,
        "Tickets" => Ticket,
        "Docs" or "Documents" => Documento,
        "Webhooks" => Webhook,
        "Teams" => Equipo,
        "Reports" => Informe,
        _ => tipo
    };
}
