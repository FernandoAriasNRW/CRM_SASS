using BuildingBlocks.Application.Abstractions;

namespace Webhook.Application.Commands;

/// <summary>
/// Comando interno que despacha el payload de un evento a todos los suscriptores activos.
/// Se emite desde cualquier módulo al ocurrir un evento relevante.
/// </summary>
public sealed record DispatchWebhookEventCommand(
    string EventName,
    Guid TenantId,
    object EventData
) : ICommand<bool>;
