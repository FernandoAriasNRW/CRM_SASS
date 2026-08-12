using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Webhook.Application.Abstractions;

namespace Webhook.Infrastructure.Persistence;

/// <summary>
/// Ata el UnitOfWork del módulo Webhook a su propio <c>DbContext</c>.
/// </summary>
public sealed class WebhookModuleUnitOfWork(WebhookDbContext context, IOutboxService outboxService)
    : UnitOfWork<WebhookDbContext>(context, outboxService), IWebhookUnitOfWork
{
}
