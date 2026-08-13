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
        DateOnly DueDate,
        // Opcional: sin prioridad explícita, la tarea nace en la de por defecto.
        string? Priority = null,
        // Opcional: si viene, la tarea nace como subtarea de ésa.
        Guid? ParentTaskId = null
    ) : ICommand<WorkTask>, IWebhookTriggered
{
    public string WebhookEventName => "workitem.created";
}
