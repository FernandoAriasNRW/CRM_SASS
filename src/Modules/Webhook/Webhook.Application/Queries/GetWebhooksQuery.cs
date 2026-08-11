
using MediatR;
using Webhook.Application.DTOs;

namespace Webhook.Application.Queries;

public record GetWebhooksQuery() : IRequest<List<WebhookDto>>;
