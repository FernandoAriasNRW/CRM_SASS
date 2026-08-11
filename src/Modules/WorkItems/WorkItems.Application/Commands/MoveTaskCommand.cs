using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using MediatR;

namespace WorkItems.Application.Commands;

public sealed record MoveTaskCommand(
    Guid TenantId,
    Guid Id,
    Guid ActorId,
    string ActorRole,
    string NewStatus
) : ICommand<bool>, IWebhookTriggered, IAuthorizeEntity
{
    public string WebhookEventName => "workitem.moved";
    
    public string EntityType => "Task";
    public Guid EntityId => Id;
    public string RequiredPermission => "Write";
}
