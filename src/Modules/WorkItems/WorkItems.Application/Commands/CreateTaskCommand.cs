using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using MediatR;
using WorkItems.Domain.Entities;

namespace WorkItems.Application.Commands;

public sealed record CreateTaskCommand(
    Guid TenantId,
    Guid CreatedById,
    Guid ProjectId,
    string Title,
    string Description,
    Guid AssigneeId,
    decimal EstimatedHours,
        DateOnly DueDate
    ) : ICommand<WorkTask>, IWebhookTriggered
{
    public string WebhookEventName => "workitem.created";
}
