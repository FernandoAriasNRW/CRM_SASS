namespace BuildingBlocks.Application.Abstractions;

/// <summary>
/// Marca un ICommand como fuente de un evento webhook.
/// El pipeline behavior lo intercepta y despacha el payload al módulo Webhook automáticamente.
/// </summary>
public interface IWebhookTriggered
{
    /// <summary>Nombre del evento tal como lo registra el suscriptor (ej: "ticket.created")</summary>
    string WebhookEventName { get; }

    /// <summary>TenantId para filtrar los suscriptores activos</summary>
    Guid TenantId { get; }
}
