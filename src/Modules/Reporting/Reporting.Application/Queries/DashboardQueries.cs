using BuildingBlocks.Application.Abstractions;
using Reporting.Application.DTOs;

namespace Reporting.Application.Queries;

public record GetKpiDataQuery(Guid TenantId) : IQuery<KpiDataDto>;

public record GetTaskBreakdownQuery(Guid TenantId) : IQuery<List<TaskStatusBreakdownDto>>;

public record GetProjectProgressQuery(Guid TenantId) : IQuery<List<ProjectProgressDto>>;

public record GetProjectBurndownQuery(Guid TenantId, Guid ProjectId) : IQuery<ProjectBurndownDto>;
