using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Application.Dashboards;
using Reporting.Application.Definitions;
using Reporting.Domain.Entities;
using Reporting.Domain.Dashboards;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Dashboards;

/// <summary>
/// Lo que se pinta en un recuadro, o por qué no se puede.
///
/// El error va <b>por widget</b>, no por panel: un informe roto apaga su recuadro y deja los otros
/// cinco funcionando. Si el fallo tumbara la petición entera, un solo informe mal configurado
/// dejaría la pantalla de inicio en blanco.
/// </summary>
public sealed record WidgetDataDto(
    Guid WidgetId,
    Guid ReportId,
    string Title,
    string Forma,
    string? Subtitle,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    string? Error);
