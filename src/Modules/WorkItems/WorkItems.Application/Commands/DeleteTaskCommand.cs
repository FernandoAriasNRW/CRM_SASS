using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using MediatR;

namespace WorkItems.Application.Commands;


public sealed record DeleteTaskCommand(
    Guid TenantId,
    Guid Id,
    Guid ActorId,
    string ActorRole
) : ICommand<bool>, IWebhookTriggered, IAuthorizeEntity
{
    public string WebhookEventName => "workitem.deleted";
    
    public string EntityType => "Task";
    public Guid EntityId => Id;
    public string RequiredPermission => "Admin"; // Require Admin to delete
}
