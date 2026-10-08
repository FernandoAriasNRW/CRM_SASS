using Notifications.Domain.Entities;

namespace Notifications.Application.Preferences;

/// <summary>
/// Las preferencias tal como viajan por la API: las vías, las horas de silencio y un ajuste por
/// cada tipo de aviso que la persona puede recibir.
///
/// <b>La lista de tipos depende del rol.</b> Quien no administra no ve los avisos de
/// administración: no los podría encender, y enseñárselos sería ofrecer algo que no puede tener.
/// </summary>
public sealed record NotificationPreferencesDto(
    bool EmailEnabled,
    bool PushEnabled,
    bool QuietHoursEnabled,
    string QuietHoursStart,
    string QuietHoursEnd,
    IReadOnlyList<NotificationTypePreferenceDto> Types)
{
    /// <summary>«HH:mm», que es lo que produce y espera un &lt;input type="time"&gt;.</summary>
    private const string TimeFormat = @"HH\:mm";

    public static NotificationPreferencesDto From(NotificationPreferences p, bool isAdmin) => new(
        p.EmailEnabled, p.PushEnabled,
        p.QuietHoursEnabled,
        p.QuietHoursStart.ToString(TimeFormat),
        p.QuietHoursEnd.ToString(TimeFormat),
        NotificationCatalog.All
            .Where(e => isAdmin || !e.AdminOnly)
            .Select(e => new NotificationTypePreferenceDto(e.Kind, e.Category, p.IsEnabled(e.Kind, isAdmin), e.AdminOnly))
            .ToList());
}

/// <summary>Un tipo de aviso: de qué área es, si está encendido y si es de administración.</summary>
public sealed record NotificationTypePreferenceDto(string Kind, string Category, bool Enabled, bool AdminOnly);

/// <summary>Lo que se quiere cambiar de un tipo de aviso.</summary>
public sealed record NotificationTypeSetting(string Kind, bool Enabled);
