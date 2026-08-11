using BuildingBlocks.Application.Abstractions;
using Reporting.Application.DTOs;

namespace Reporting.Application.Queries;

public sealed record GetReportByIdQuery(Guid TenantId, Guid ReportId) : IQuery<ReportDto?>;
