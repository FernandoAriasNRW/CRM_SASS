using MediatR;

namespace BuildingBlocks.Infrastructure.Webhooks.Webhook.Application;

public record DispatchWebhookCommand(string Event, string Payload) : IRequest;