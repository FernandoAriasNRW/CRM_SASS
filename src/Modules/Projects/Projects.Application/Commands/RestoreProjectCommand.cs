using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Projects.Domain.Entities;

namespace Projects.Application.Commands;

public sealed record RestoreProjectCommand(
    Guid TenantId,
    Guid Id
) : ICommand<Project>, IWebhookTriggered
{
    public string WebhookEventName => "project.restored";
}

