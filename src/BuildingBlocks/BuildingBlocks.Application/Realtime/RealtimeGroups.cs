namespace BuildingBlocks.Application.Realtime;

/// <summary>
/// Nombres de los grupos de SignalR. Todos empiezan por el inquilino.
///
/// Los hubs metían la conexión en el grupo que pidiera el cliente —<c>JoinTickets(tenantId)</c>,
/// <c>JoinBoard(projectId)</c>, <c>JoinChannel(channelId)</c>— sin mirar de quién era, así que
/// bastaba con saber un identificador de otra organización para escuchar sus cambios en tiempo
/// real. Ahora el hub pone el inquilino del token y el cliente sólo elige el proyecto o el canal:
/// quien pida un proyecto ajeno entra en un grupo que lleva su propio inquilino delante, y a ése
/// no se manda nunca nada.
///
/// El que emite usa los mismos nombres con el inquilino del evento, de modo que los dos lados
/// sólo coinciden cuando el inquilino es el mismo.
/// </summary>
public static class RealtimeGroups
{
    /// <summary>Todas las conexiones de una organización (el tablero de tickets).</summary>
    public static string Tenant(Guid tenantId) => $"tenant:{tenantId}";

    /// <summary>
    /// Todas las tareas de una organización. Es al que se une el tablero de tareas, sea cual sea su
    /// ámbito —mías, de un proyecto, de un equipo, de toda la organización—: la pantalla sólo
    /// actualiza las tarjetas que tiene delante, y así no hace falta un grupo por cada forma de
    /// filtrar. Cualquiera de la organización puede ver sus tareas, así que no enseña nada de más.
    /// </summary>
    public static string Tasks(Guid tenantId) => $"tenant:{tenantId}:tasks";

    /// <summary>El tablero de un proyecto.</summary>
    public static string Board(Guid tenantId, Guid projectId) => $"tenant:{tenantId}:board:{projectId}";

    /// <summary>Un canal de chat.</summary>
    public static string Channel(Guid tenantId, Guid conversationId) => $"tenant:{tenantId}:channel:{conversationId}";
}
