using BuildingBlocks.Domain.Primitives;
using Webhook.Domain.Events;

namespace Webhook.Domain.Entities;

/// <summary>
/// Un destino al que la organización manda sus eventos: un nombre, una URL, el secreto con que se
/// firma cada envío y la lista de eventos que quiere.
///
/// <b>Una suscripción, varios eventos.</b> Antes era una fila por evento: para recibir lo que pasa
/// con las tareas había que crear catorce suscripciones con la misma URL y el mismo secreto, y la
/// pantalla —que ya pedía una lista— no podía guardar nada de lo que enseñaba.
///
/// <b>Nunca «todos» por defecto.</b> La lista no puede estar vacía y no admite comodines: quien la
/// crea elige uno a uno los eventos que salen de la organización.
/// </summary>
public sealed class WebhookSubscription : AggregateRoot, ITenantEntity
{
    public const int NameMaxLength = 100;
    public const int UrlMaxLength = 2048;

    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string TargetUrl { get; private set; } = string.Empty;
    public string Secret { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private List<string> _eventTypes = [];

    /// <summary>Los eventos a los que está suscrita. Ver <see cref="WebhookEventCatalog"/>.</summary>
    public IReadOnlyList<string> EventTypes => _eventTypes.AsReadOnly();

    private WebhookSubscription() { }

    public static WebhookSubscription Create(
        DateTime nowUtc, Guid tenantId, string name, string targetUrl, IEnumerable<string> eventTypes, string secret)
    {
        var subscription = new WebhookSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Secret = RequireSecret(secret),
            IsActive = true,
            CreatedAt = nowUtc,
        };
        subscription.Apply(name, targetUrl, eventTypes);

        subscription.RaiseDomainEvent(new WebhookSubscriptionCreatedEvent(subscription.Id, tenantId));
        return subscription;
    }

    public void Update(DateTime nowUtc, string name, string targetUrl, IEnumerable<string> eventTypes, bool isActive)
    {
        Apply(name, targetUrl, eventTypes);
        IsActive = isActive;
        UpdatedAt = nowUtc;

        RaiseDomainEvent(new WebhookSubscriptionUpdatedEvent(Id, TenantId));
    }

    /// <summary>
    /// Cambia el secreto. El anterior deja de valer en el acto: es lo que se hace cuando se ha
    /// filtrado, y una ventana en la que valen los dos sería justo lo que hay que evitar.
    /// </summary>
    public void RegenerateSecret(DateTime nowUtc, string secret)
    {
        Secret = RequireSecret(secret);
        UpdatedAt = nowUtc;
    }

    public bool Subscribes(string eventName) => IsActive && _eventTypes.Contains(eventName);

    private void Apply(string name, string targetUrl, IEnumerable<string> eventTypes)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        if (trimmedName.Length == 0) throw new InvalidOperationException(Rules.NameRequired);
        if (trimmedName.Length > NameMaxLength) throw new InvalidOperationException(Rules.NameTooLong);

        var url = (targetUrl ?? string.Empty).Trim();
        if (url.Length == 0 || url.Length > UrlMaxLength
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new InvalidOperationException(Rules.InvalidUrl);

        var events = (eventTypes ?? []).Select(e => (e ?? string.Empty).Trim()).Where(e => e.Length > 0).Distinct().ToList();
        if (events.Count == 0) throw new InvalidOperationException(Rules.EventsRequired);

        var unknown = events.Where(e => !WebhookEventCatalog.Exists(e)).ToList();
        if (unknown.Count > 0) throw new InvalidOperationException(Rules.UnknownEvents + string.Join(", ", unknown));

        Name = trimmedName;
        TargetUrl = url;
        _eventTypes = events;
    }

    private static string RequireSecret(string secret)
        => string.IsNullOrWhiteSpace(secret) ? throw new InvalidOperationException(Rules.SecretRequired) : secret;

    public static class Rules
    {
        public const string NameRequired = "El webhook necesita un nombre";
        public static readonly string NameTooLong = $"El nombre no puede pasar de {NameMaxLength} caracteres";
        public const string InvalidUrl = "La URL tiene que ser una dirección http o https completa";
        public const string EventsRequired = "Elige al menos un evento: un webhook no se suscribe a todo por defecto";
        public const string UnknownEvents = "Estos eventos no existen: ";
        public const string SecretRequired = "El webhook necesita un secreto para firmar los envíos";
        public const string NotFound = "Webhook no encontrado";
    }
}
