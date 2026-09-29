using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Definitions;

namespace Reporting.Application.Definitions;

/// <summary>
/// Resuelve una definición a filas.
///
/// <b>Es un puerto</b>, igual que <c>IDashboardRepository</c> y por la misma razón: resolver un
/// informe de tareas exige mirar WorkItems y uno de tickets exige mirar Ticketing, y ningún
/// módulo referencia a otro. Reporting dice qué necesita; el host, que los conoce a todos, lo
/// satisface.
/// </summary>
public interface IReportResolver
{
    /// <summary>
    /// Los valores admitidos de un campo de lista cerrada, o vacío si no lo es.
    ///
    /// Va en este puerto y no en el catálogo porque los valores viven en otros módulos —los
    /// estados de un ticket son de Ticketing— y Reporting no los puede conocer. El host, que los
    /// conoce a todos, los aporta.
    /// </summary>
    IReadOnlyList<string> ValuesOf(string dataSource, string field);

    Task<Exports.ReportTable> ResolveAsync(
        string title, Guid tenantId, ReportDefinition definition, CancellationToken ct = default);
}
