using BuildingBlocks.Application.Abstractions;
using WorkItems.Application.DTOs;

namespace WorkItems.Application.Queries;

public sealed record GetChecklistQuery(
    Guid TenantId,
    Guid TaskId
) : IQuery<IReadOnlyList<ChecklistItemDto>>;
