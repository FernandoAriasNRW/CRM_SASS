namespace WorkItems.Application.DTOs;

public sealed record TaskDto(
    Guid Id,
    Guid TenantId,
    Guid ProjectId,
    string Title,
    string Description,
    string Status,
    string Priority,
    Guid AssigneeId,
    Guid CreatedById,
    decimal EstimatedHours,
    DateOnly DueDate
);
