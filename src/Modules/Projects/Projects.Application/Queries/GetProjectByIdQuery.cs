using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Projects.Application.DTOs;

namespace Projects.Application.Queries;

public sealed record GetProjectByIdQuery(Guid TenantId, Guid Id) : IQuery<ProjectDto?>;
