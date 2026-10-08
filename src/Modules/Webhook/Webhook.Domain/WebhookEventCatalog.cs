namespace Webhook.Domain;

/// <summary>
/// Los eventos a los que se puede suscribir un webhook, en un solo sitio.
///
/// <b>Cada nombre lo emite algún comando</b> (<c>IWebhookTriggered.WebhookEventName</c>), y
/// <c>WebhookEventCatalogTests</c> comprueba las dos direcciones: que todo lo que se emite está
/// aquí, y que todo lo que está aquí lo emite alguien. Antes había una lista con nombres que nadie
/// emitía —«report.generated»— y comandos que emitían nombres que no estaban en ella: una
/// suscripción podía elegir un evento que no llegaba nunca, sin ningún aviso.
///
/// <b>No hay comodín.</b> Una suscripción nombra los eventos que quiere, uno a uno: suscribirse a
/// «todo» por defecto mandaría fuera de la organización datos que nadie decidió enviar.
///
/// Los nombres viajan en la cabecera <c>X-Webhook-Event</c> y se guardan en las suscripciones:
/// cambiar uno exige migrar los datos en el mismo cambio.
/// </summary>
public static class WebhookEventCatalog
{
    /// <summary>El de prueba: sólo lo manda «Enviar prueba», para comprobar que el destino responde.</summary>
    public const string Test = "webhook.test";

    /// <summary>Un evento del catálogo: su nombre y el grupo en que se enseña.</summary>
    public sealed record Entry(string Name, string Category);

    public static class Categories
    {
        public const string Tasks = "tasks";
        public const string Tickets = "tickets";
        public const string Projects = "projects";
        public const string Users = "users";
        public const string Teams = "teams";
        public const string Reports = "reports";
        public const string Documents = "documents";
        public const string Notifications = "notifications";
        public const string Calendar = "calendar";
        public const string Chat = "chat";
    }

    public static IReadOnlyList<Entry> All { get; } =
    [
        new("task.created", Categories.Tasks),
        new("task.updated", Categories.Tasks),
        new("task.status_changed", Categories.Tasks),
        new("task.deleted", Categories.Tasks),
        new("task.reparented", Categories.Tasks),
        new("task.assignee.added", Categories.Tasks),
        new("task.assignee.removed", Categories.Tasks),
        new("task.dependency.added", Categories.Tasks),
        new("task.dependency.removed", Categories.Tasks),
        new("task.checklist.added", Categories.Tasks),
        new("task.checklist.updated", Categories.Tasks),
        new("task.checklist.removed", Categories.Tasks),
        new("task.recurrence.set", Categories.Tasks),
        new("task.recurrence.cleared", Categories.Tasks),
        new("task.document.attached", Categories.Tasks),
        new("task.document.detached", Categories.Tasks),

        new("ticket.created", Categories.Tickets),
        new("ticket.updated", Categories.Tickets),
        new("ticket.status_changed", Categories.Tickets),
        new("ticket.assigned", Categories.Tickets),
        new("ticket.closed", Categories.Tickets),

        new("project.created", Categories.Projects),
        new("project.updated", Categories.Projects),
        new("project.deleted", Categories.Projects),
        new("project.restored", Categories.Projects),

        new("user.created", Categories.Users),
        new("user.updated", Categories.Users),
        new("user.deleted", Categories.Users),

        new("team.created", Categories.Teams),
        new("team.updated", Categories.Teams),
        new("team.deleted", Categories.Teams),

        new("report.created", Categories.Reports),

        new("document.created", Categories.Documents),
        new("document.updated", Categories.Documents),
        new("document.deleted", Categories.Documents),

        new("notification.created", Categories.Notifications),
        new("notification.read", Categories.Notifications),
        new("notification.deleted", Categories.Notifications),

        new("calendar.event.created", Categories.Calendar),
        new("calendar.event.updated", Categories.Calendar),
        new("calendar.event.rescheduled", Categories.Calendar),
        new("calendar.event.cancelled", Categories.Calendar),
        new("calendar.event.linked", Categories.Calendar),
        new("calendar.event.trashed", Categories.Calendar),
        new("calendar.event.restored", Categories.Calendar),

        new("chat.conversation.created", Categories.Chat),
        new("chat.conversation.deleted", Categories.Chat),
        new("chat.message.sent", Categories.Chat),
        new("chat.message.edited", Categories.Chat),
        new("chat.message.deleted", Categories.Chat),
    ];

    public static bool Exists(string? name) => name is not null && All.Any(e => e.Name == name);
}
