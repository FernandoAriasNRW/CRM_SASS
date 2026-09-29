using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Definitions;

namespace Reporting.Application.Definitions;

/// <summary>
/// La vista previa del constructor: enseña el resultado **antes** de guardar.
///
/// Sin ella, construir un informe es escribir a ciegas y descubrir el resultado al exportarlo,
/// que es cuando ya se ha guardado y quien lo hizo se ha ido. Se limita a unas pocas filas: es
/// una comprobación de que la definición dice lo que se pretendía, no el informe entero.
/// </summary>
public sealed record PreviewQuery(
    Guid TenantId, string Title, ReportDefinition Definition) : IQuery<PreviewDto>;
