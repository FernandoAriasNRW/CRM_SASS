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
/// Los datos de <b>todos</b> los recuadros del panel, en una sola petición.
///
/// <b>Es la segunda decisión del plan, y la razón es de rendimiento y de coherencia a la vez:</b>
/// «un widget que trae sus propios datos con su propia llamada convierte el dashboard en veinte
/// peticiones; una consulta declarada permite pedirlas juntas». Además, pidiéndolas juntas todos
/// los recuadros son de la misma foto: con veinte llamadas escalonadas, el de arriba puede contar
/// tickets de antes de que llegara uno nuevo y el de abajo de después, y los números no cuadran
/// entre sí.
/// </summary>
public sealed record GetDashboardDataQuery(Guid TenantId, Guid PanelId) : IQuery<IReadOnlyList<WidgetDataDto>>;
