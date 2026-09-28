using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Notifications.Domain.Entities;

namespace Notifications.Application.Preferences;

/// <summary>
/// Las preferencias tal como viajan por la API. Los nombres son los que ya usaba la pantalla.
/// </summary>
public sealed record NotificationPreferencesDto(
    bool EmailEnabled,
    bool PushEnabled,
    bool TaskAssigned,
    bool TaskCompleted,
    bool TaskDueSoon,
    bool TicketCreated,
    bool TicketUpdated,
    bool ProjectUpdated,
    bool MentionEnabled,
    bool ExportReady,
    bool QuietHoursEnabled,
    string QuietHoursStart,
    string QuietHoursEnd)
{
    /// <summary>«HH:mm», que es lo que produce y espera un &lt;input type="time"&gt;.</summary>
    private const string TimeFormat = "HH\\:mm";

    public static NotificationPreferencesDto From(NotificationPreferences p) => new(
        p.EmailEnabled, p.PushEnabled,
        p.TaskAssigned, p.TaskCompleted, p.TaskDueSoon,
        p.TicketCreated, p.TicketUpdated, p.ProjectUpdated,
        p.MentionEnabled, p.ExportReady,
        p.QuietHoursEnabled,
        p.QuietHoursStart.ToString(TimeFormat),
        p.QuietHoursEnd.ToString(TimeFormat));
}
