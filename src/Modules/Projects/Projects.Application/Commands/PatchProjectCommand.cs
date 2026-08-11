using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;

namespace Projects.Application.Commands;

public sealed record PatchProjectCommand(
    Guid TenantId,
    Guid Id,
    string? Name,
    string? Description,
    string? Status,
    DateOnly? EstimatedEndDate
) : ICommand<bool>, IWebhookTriggered
{
    public string WebhookEventName => "project.updated";
}
