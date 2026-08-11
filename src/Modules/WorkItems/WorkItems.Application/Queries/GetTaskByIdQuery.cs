using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using WorkItems.Application.DTOs;

namespace WorkItems.Application.Queries;

public sealed record GetTaskByIdQuery(Guid TenantId, Guid Id) : IQuery<TaskDto?>;
