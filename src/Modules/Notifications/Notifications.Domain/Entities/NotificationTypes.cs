using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;

namespace Notifications.Domain.Entities;

/// <summary>
/// Los tipos de aviso que se pueden silenciar, en un solo sitio.
/// </summary>
public static class NotificationTypes
{
    public const string TaskAssigned = "TaskAssigned";
    public const string TaskCompleted = "TaskCompleted";
    /// <summary>
    /// Una tarea se acerca a su vencimiento.
    ///
    /// Lo usan dos cosas distintas: el aviso propio del producto y las automatizaciones por
    /// tiempo que alguien configure. Comparten preferencia a propósito: para quien lo recibe es
    /// el mismo aviso —«esto va a llegar tarde»—, y que llegue o no según quién lo originara
    /// sería una distinción que sólo entiende quien programó esto.
    /// </summary>
    public const string TaskDueSoon = "TaskDueSoon";
    public const string TicketCreated = "TicketCreated";
    public const string TicketUpdated = "TicketUpdated";
    public const string ProjectUpdated = "ProjectUpdated";
    public const string Mention = "Mention";
    public const string ExportReady = "ExportReady";
}
