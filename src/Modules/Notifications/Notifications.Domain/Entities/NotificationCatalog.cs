namespace Notifications.Domain.Entities;

/// <summary>
/// Los tipos de aviso, en un solo sitio: de qué área son, si vienen encendidos y quién los puede
/// recibir.
///
/// <b>Encendido de serie, lo que la persona espera.</b> Que le asignen algo, que cambie algo suyo,
/// que la mencionen, que comenten en lo que creó. Apagado, lo que es ruido: cada edición de un
/// campo. Cada persona lo cambia a su gusto en sus preferencias.
///
/// <b>Algunos son sólo de administración</b> (<see cref="Entry.AdminOnly"/>): quien no administra no
/// los ve en sus preferencias, no los puede encender y no los recibe aunque la fila diga otra cosa.
///
/// Un tipo sólo entra aquí cuando algo lo manda: ofrecer en la pantalla un aviso que no llega
/// nunca es prometer y no cumplir.
/// </summary>
public static class NotificationCatalog
{
    public sealed record Entry(string Kind, string Category, bool DefaultEnabled, bool AdminOnly = false);

    public static class Categories
    {
        public const string Tasks = "tasks";
        public const string Tickets = "tickets";
        public const string Projects = "projects";
        public const string Mentions = "mentions";
        public const string Reports = "reports";
    }

    // ── Tareas ───────────────────────────────────────────────────────────────────────────────
    public const string TaskAssigned = "task.assigned";
    public const string TaskStatusChanged = "task.status_changed";
    public const string TaskCompleted = "task.completed";
    public const string TaskUpdated = "task.updated";
    public const string TaskDeleted = "task.deleted";
    public const string TaskCommented = "task.commented";
    public const string TaskDueSoon = "task.due_soon";

    // ── Tickets ──────────────────────────────────────────────────────────────────────────────
    public const string TicketAssigned = "ticket.assigned";
    public const string TicketStatusChanged = "ticket.status_changed";
    public const string TicketUpdated = "ticket.updated";
    public const string TicketCommented = "ticket.commented";

    // ── Proyectos ────────────────────────────────────────────────────────────────────────────
    public const string ProjectUpdated = "project.updated";
    public const string ProjectDeleted = "project.deleted";
    public const string ProjectCommented = "project.commented";

    // ── Menciones ────────────────────────────────────────────────────────────────────────────
    public const string Mention = "mention";

    // ── Informes ─────────────────────────────────────────────────────────────────────────────
    public const string ExportReady = "report.export_ready";
    public const string ExportFailed = "report.export_failed";

    public static IReadOnlyList<Entry> All { get; } =
    [
        new(TaskAssigned, Categories.Tasks, true),
        new(TaskStatusChanged, Categories.Tasks, true),
        new(TaskCompleted, Categories.Tasks, true),
        new(TaskUpdated, Categories.Tasks, false),
        new(TaskDeleted, Categories.Tasks, true),
        new(TaskCommented, Categories.Tasks, true),
        new(TaskDueSoon, Categories.Tasks, true),

        new(TicketAssigned, Categories.Tickets, true),
        new(TicketStatusChanged, Categories.Tickets, true),
        new(TicketUpdated, Categories.Tickets, false),
        new(TicketCommented, Categories.Tickets, true),

        new(ProjectUpdated, Categories.Projects, true),
        new(ProjectDeleted, Categories.Projects, true),
        new(ProjectCommented, Categories.Projects, true),

        new(Mention, Categories.Mentions, true),

        new(ExportReady, Categories.Reports, true),
        new(ExportFailed, Categories.Reports, true),
    ];

    public static Entry? Find(string? kind) => All.FirstOrDefault(e => e.Kind == kind);
}
