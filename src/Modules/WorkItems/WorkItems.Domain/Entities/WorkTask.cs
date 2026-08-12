using BuildingBlocks.Domain.Primitives;
using WorkItems.Domain.Events;
using WorkItems.Domain.ValueObjects;
using TaskStatus = WorkItems.Domain.ValueObjects.TaskStatus;

namespace WorkItems.Domain.Entities;

public sealed class WorkTask : AggregateRoot, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public TaskTitle Title { get; private set; } = null!;
    public string Description { get; private set; } = string.Empty;
    public TaskStatus Status { get; private set; } = null!;
    public Guid AssigneeId { get; private set; }
    public Guid CreatedById { get; private set; }
    public decimal EstimatedHours { get; private set; }
    public DateOnly DueDate { get; private set; }
    public List<Guid> TagIds { get; private set; } = new();

    private WorkTask() { }

    public static WorkTask Create(
        Guid tenantId,
        Guid projectId,
        string title,
        string description,
        Guid assigneeId,
        Guid createdById,
        decimal estimatedHours,
        DateOnly dueDate)
    {
        var task = new WorkTask
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            Title = TaskTitle.Create(title).Value!,
            Description = description,
            Status = TaskStatus.ToDo,
            AssigneeId = assigneeId,
            CreatedById = createdById,
            EstimatedHours = estimatedHours,
            DueDate = dueDate
        };

        task.RaiseDomainEvent(new TaskCreatedEvent(task.Id, tenantId, projectId, assigneeId));

        return task;
    }

    /// <summary>
    /// Cambia el estado de la tarea.
    ///
    /// Se admite cualquier estado existente, sin restringir desde cuál se viene: qué
    /// movimiento tiene sentido lo decide quien gestiona el trabajo. Sólo se rechaza un
    /// estado que no exista, que sería un dato corrupto.
    /// </summary>
    public void Move(string newStatus)
    {
        if (!TaskStatus.Existe(newStatus))
            throw new InvalidOperationException($"El estado '{newStatus}' no existe");

        var oldStatus = Status;
        Status = new TaskStatus(newStatus, newStatus);

        RaiseDomainEvent(new TaskStatusChangedEvent(Id, TenantId, ProjectId, oldStatus.Value.ToString(), newStatus));
    }

    public void Assign(Guid assigneeId)
    {
        AssigneeId = assigneeId;
        RaiseDomainEvent(new TaskAssignedEvent(Id, TenantId, assigneeId));
    }

    public void AddTag(Guid tagId)
    {
        if (!TagIds.Contains(tagId))
        {
            TagIds.Add(tagId);
            // Optionally raise domain event
        }
    }

    public void RemoveTag(Guid tagId)
    {
        if (TagIds.Contains(tagId))
        {
            TagIds.Remove(tagId);
        }
    }
}
