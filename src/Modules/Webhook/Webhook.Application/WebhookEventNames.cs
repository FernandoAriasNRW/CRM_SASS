namespace Webhook.Application;

/// <summary>
/// Catálogo de nombres de evento disponibles para suscripción.
/// Estos son los valores exactos que el suscriptor configura en EventName al crear un WebhookSubscription.
/// </summary>
public static class WebhookEventNames
{
    // ── Identity ──────────────────────────────────────────
    public const string UserRegistered  = "identity.user.registered";
    public const string UserUpdated     = "identity.user.updated";
    public const string UserDeleted     = "identity.user.deleted";

    // ── Projects ──────────────────────────────────────────
    public const string ProjectCreated  = "project.created";
    public const string ProjectUpdated  = "project.updated";
    public const string ProjectDeleted  = "project.deleted";
    public const string ProjectRestored = "project.restored";

    // ── WorkItems ─────────────────────────────────────────
    public const string TaskCreated     = "workitem.created";
    public const string TaskMoved       = "workitem.moved";
    public const string TaskPatched     = "workitem.patched";
    public const string TaskDeleted     = "workitem.deleted";

    // ── Ticketing ─────────────────────────────────────────
    public const string TicketCreated        = "ticket.created";
    public const string TicketUpdated        = "ticket.updated";
    public const string TicketStatusChanged  = "ticket.status_changed";
    public const string TicketAssigned       = "ticket.assigned";
    public const string TicketClosed         = "ticket.closed";

    // ── Notifications ─────────────────────────────────────
    public const string NotificationCreated = "notification.created";
    public const string NotificationRead    = "notification.read";
    public const string NotificationDeleted = "notification.deleted";

    // ── Calendar ──────────────────────────────────────────
    public const string CalendarEventCreated     = "calendar.event.created";
    public const string CalendarEventUpdated     = "calendar.event.updated";
    public const string CalendarEventRescheduled = "calendar.event.rescheduled";
    public const string CalendarEventCancelled   = "calendar.event.cancelled";
    public const string CalendarEventRestored    = "calendar.event.restored";

    // ── Communication ─────────────────────────────────────
    public const string ConversationCreated = "communication.conversation.created";
    public const string ConversationDeleted = "communication.conversation.deleted";
    public const string MessageSent         = "communication.message.sent";
    public const string MessageEdited       = "communication.message.edited";
    public const string MessageDeleted      = "communication.message.deleted";

    // ── Reporting ─────────────────────────────────────────
    public const string ReportCreated    = "report.created";
    public const string ReportGenerated  = "report.generated";
}
