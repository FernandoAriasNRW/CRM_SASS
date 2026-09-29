using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;

namespace Notifications.Domain.Entities;

/// <summary>
/// Qué avisos quiere recibir una persona, y por qué vía.
///
/// **No existían.** `GET /notifications/preferences` devolvía un objeto con valores fijos
/// escritos en el código y `PUT` respondía con lo mismo que recibía, sin guardar nada: la
/// pantalla de preferencias funcionaba, se podían mover todos los interruptores, y al recargar
/// volvían a su sitio. Peor que no tener la pantalla, porque prometía algo que no cumplía.
///
/// Hay una fila por persona y organización. El aislamiento entre inquilinos lo aplica el filtro
/// global, igual que al resto de entidades del módulo.
///
/// **Todo llega activado salvo lo que molesta.** El criterio: un aviso que la persona espera
/// —le asignan algo, la mencionan, su exportación terminó— viene encendido, porque no recibirlo
/// se vive como que el sistema falla. Los que informan de actividad ajena —una tarea que otro
/// completó, un ticket que otro tocó— vienen apagados, porque encendidos hacen ruido y el ruido
/// acaba con la persona ignorando *todos* los avisos, incluidos los que sí importaban.
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

    // ── Sobre el trabajo propio ───────────────────────────────────────────────────────────
    public bool TaskAssigned { get; private set; }
    public bool TaskDueSoon { get; private set; }
    public bool MentionEnabled { get; private set; }

    /// <summary>
    /// El aviso de que una exportación terminó.
    ///
    /// Encendido de origen porque es la respuesta a algo que la persona pidió: las
    /// exportaciones se generan en segundo plano y pueden tardar, así que sin aviso hay que
    /// volver a mirar la pantalla cada poco. Se puede apagar como cualquier otro; era una
    /// condición explícita del encargo.
    /// </summary>
    public bool ExportReady { get; private set; }

    // ── Sobre el trabajo de los demás ─────────────────────────────────────────────────────
    public bool TaskCompleted { get; private set; }
    public bool TicketCreated { get; private set; }
    public bool TicketUpdated { get; private set; }
    public bool ProjectUpdated { get; private set; }

    // ── Horas de silencio ─────────────────────────────────────────────────────────────────
    public bool QuietHoursEnabled { get; private set; }

    /// <summary>
    /// Se guardan como <see cref="TimeOnly"/> y no como texto: «22:00» es una hora, y dejarla
    /// en una cadena invita a que llegue «10 PM», «22.00» o vacío, y a descubrirlo al comparar.
    /// </summary>
    public TimeOnly QuietHoursStart { get; private set; }
    public TimeOnly QuietHoursEnd { get; private set; }

    private NotificationPreferences() { }

    /// <summary>
    /// Las preferencias de quien nunca las ha tocado. Ver arriba el criterio de qué nace
    /// encendido.
    /// </summary>
    public static NotificationPreferences CreateDefault(Guid tenantId, Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        UserId = userId,

        EmailEnabled = true,
        PushEnabled = false,

        TaskAssigned = true,
        TaskDueSoon = true,
        MentionEnabled = true,
        ExportReady = true,

        TaskCompleted = false,
        TicketCreated = true,
        TicketUpdated = false,
        ProjectUpdated = true,

        QuietHoursEnabled = false,
        QuietHoursStart = new TimeOnly(22, 0),
        QuietHoursEnd = new TimeOnly(8, 0),
    };

    public void Update(
        bool emailEnabled, bool pushEnabled,
        bool taskAssigned, bool taskDueSoon, bool mentionEnabled, bool exportReady,
        bool taskCompleted, bool ticketCreated, bool ticketUpdated, bool projectUpdated,
        bool quietHoursEnabled, TimeOnly quietHoursStart, TimeOnly quietHoursEnd)
    {
        EmailEnabled = emailEnabled;
        PushEnabled = pushEnabled;

        TaskAssigned = taskAssigned;
        TaskDueSoon = taskDueSoon;
        MentionEnabled = mentionEnabled;
        ExportReady = exportReady;

        TaskCompleted = taskCompleted;
        TicketCreated = ticketCreated;
        TicketUpdated = ticketUpdated;
        ProjectUpdated = projectUpdated;

        QuietHoursEnabled = quietHoursEnabled;
        QuietHoursStart = quietHoursStart;
        QuietHoursEnd = quietHoursEnd;
    }

    /// <summary>
    /// Si un aviso de este tipo debe llegar ahora mismo.
    ///
    /// Función pura, con la hora como argumento y no leída del reloj, para que se pueda probar
    /// la medianoche sin esperar a que sean las doce.
    /// </summary>
    public bool ShouldDeliver(string type, TimeOnly now)
    {
        if (!IsEnabled(type)) return false;

        return !QuietHoursEnabled || !IsQuietAt(now);
    }

    public bool IsEnabled(string type) => type switch
    {
        NotificationTypes.TaskAssigned => TaskAssigned,
        NotificationTypes.TaskCompleted => TaskCompleted,
        NotificationTypes.TaskDueSoon => TaskDueSoon,
        NotificationTypes.TicketCreated => TicketCreated,
        NotificationTypes.TicketUpdated => TicketUpdated,
        NotificationTypes.ProjectUpdated => ProjectUpdated,
        NotificationTypes.Mention => MentionEnabled,
        NotificationTypes.ExportReady => ExportReady,

        // Un tipo que nadie ha declarado pasa. Es deliberado: si mañana alguien añade un aviso
        // y se olvida de ponerlo en esta lista, el fallo es que se recibe de más —molesto y
        // visible— y no que se pierde en silencio, que es el fallo que nadie detecta.
        _ => true,
    };

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
}
