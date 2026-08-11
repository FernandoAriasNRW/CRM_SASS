namespace Webhook.Application.DTOs;

public sealed class WebhookDto
{
    public Guid Id { get; init; }
    public string Url { get; init; } = string.Empty;
    public string Event { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}
