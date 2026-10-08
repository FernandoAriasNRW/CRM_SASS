using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;

namespace Notifications.Domain.Entities;

/// <summary>
/// Qué avisos quiere recibir una persona, y por qué vía.
///
/// Hay una fila por persona y organización. El aislamiento entre inquilinos lo aplica el filtro
/// global, igual que al resto de entidades del módulo.
///
/// <b>Un ajuste por tipo de aviso, no un campo por tipo.</b> Eran once columnas fijas: cada aviso
/// nuevo pedía una migración, y los que no tenían columna pasaban siempre sin poderse apagar. Ahora
/// se guarda sólo lo que la persona cambió respecto a lo que trae el catálogo
/// (<see cref="NotificationCatalog"/>): un tipo que nunca tocó sigue lo que diga el catálogo, también
/// si el catálogo cambia de criterio más adelante.
///
/// <b>Con límites según el rol.</b> Los avisos de administración no los puede encender quien no
/// administra, y aunque la fila los tuviera encendidos —un rol que cambió—, no le llegan.
/// </summary>
public sealed class NotificationPreferences : AggregateRoot, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }

    // ── Vías ──────────────────────────────────────────────────────────────────────────────
    public bool EmailEnabled { get; private set; }

    /// <summary>
    /// Apagado de origen a propósito: el aviso del navegador exige un permiso que hay que
    /// conceder, y darlo por supuesto dejaría la preferencia diciendo «sí» mientras el
    /// navegador dice «no».
    /// </summary>
    public bool PushEnabled { get; private set; }

    // ── Por tipo de aviso ─────────────────────────────────────────────────────────────────
    private Dictionary<string, bool> _types = [];

    /// <summary>Lo que la persona cambió respecto al catálogo. Lo que no está aquí, sigue al catálogo.</summary>
    public IReadOnlyDictionary<string, bool> Types => _types;

    // ── Horas de silencio ─────────────────────────────────────────────────────────────────
    public bool QuietHoursEnabled { get; private set; }

    /// <summary>
    /// Se guardan como <see cref="TimeOnly"/> y no como texto: «22:00» es una hora, y dejarla
    /// en una cadena invita a que llegue «10 PM», «22.00» o vacío, y a descubrirlo al comparar.
    /// </summary>
    public TimeOnly QuietHoursStart { get; private set; }
    public TimeOnly QuietHoursEnd { get; private set; }

    private NotificationPreferences() { }

    /// <summary>Las preferencias de quien nunca las ha tocado: todo lo que dice el catálogo.</summary>
    public static NotificationPreferences CreateDefault(Guid tenantId, Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        UserId = userId,
        EmailEnabled = true,
        PushEnabled = false,
        QuietHoursEnabled = false,
        QuietHoursStart = new TimeOnly(22, 0),
        QuietHoursEnd = new TimeOnly(8, 0),
    };

    public void SetChannels(bool emailEnabled, bool pushEnabled)
    {
        EmailEnabled = emailEnabled;
        PushEnabled = pushEnabled;
    }

    public void SetQuietHours(bool enabled, TimeOnly start, TimeOnly end)
    {
        QuietHoursEnabled = enabled;
        QuietHoursStart = start;
        QuietHoursEnd = end;
    }

    /// <summary>
    /// Enciende o apaga un tipo de aviso.
    ///
    /// Un tipo que no existe se rechaza —se guardaría y no serviría para nada— y uno de
    /// administración también, si quien lo pide no administra.
    /// </summary>
    public Result SetType(string kind, bool enabled, bool isAdmin)
    {
        var entry = NotificationCatalog.Find(kind);
        if (entry is null) return Result.Failure(Rules.UnknownKind + kind);
        if (entry.AdminOnly && !isAdmin) return Result.Failure(Rules.AdminOnly + kind);

        // Sólo se guarda lo que difiere del catálogo, para que lo que no se tocó siga su criterio.
        if (enabled == entry.DefaultEnabled) _types.Remove(kind);
        else _types[kind] = enabled;

        return Result.Success();
    }

    /// <summary>
    /// Si este tipo de aviso está encendido para esta persona. Un tipo que no está en el catálogo
    /// no se manda: no hay forma de apagarlo, y algo que no se puede apagar acaba en ruido.
    /// </summary>
    public bool IsEnabled(string kind, bool isAdmin)
    {
        var entry = NotificationCatalog.Find(kind);
        if (entry is null) return false;
        if (entry.AdminOnly && !isAdmin) return false;

        return _types.TryGetValue(kind, out var enabled) ? enabled : entry.DefaultEnabled;
    }

    /// <summary>
    /// Si un aviso de este tipo debe llegar ahora mismo.
    ///
    /// Función pura, con la hora como argumento y no leída del reloj, para que se pueda probar
    /// la medianoche sin esperar a que sean las doce.
    /// </summary>
    public bool ShouldDeliver(string kind, TimeOnly now, bool isAdmin)
        => IsEnabled(kind, isAdmin) && (!QuietHoursEnabled || !IsQuietAt(now));

    /// <summary>
    /// Si la hora cae dentro del silencio.
    ///
    /// El tramo puede cruzar la medianoche —de 22:00 a 08:00 es lo normal— y entonces la
    /// comparación se invierte: dentro del silencio es «después del inicio **o** antes del
    /// fin», no «y». Escribirlo como un solo `&amp;&amp;` deja el caso habitual sin silenciar
    /// nada y el fallo sólo se ve de madrugada.
    /// </summary>
    public bool IsQuietAt(TimeOnly now) =>
        QuietHoursStart <= QuietHoursEnd
            ? now >= QuietHoursStart && now < QuietHoursEnd
            : now >= QuietHoursStart || now < QuietHoursEnd;

    public static class Rules
    {
        public const string UnknownKind = "Ese tipo de aviso no existe: ";
        public const string AdminOnly = "Ese aviso es sólo para quien administra: ";
    }
}
