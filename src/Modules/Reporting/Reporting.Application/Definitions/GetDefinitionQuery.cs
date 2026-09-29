using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Definitions;

namespace Reporting.Application.Definitions;

/// <summary>La definición guardada de un informe, para volver a abrirla en el constructor.</summary>
public sealed record GetDefinitionQuery(Guid TenantId, Guid ReportId) : IQuery<ReportDefinition>;
