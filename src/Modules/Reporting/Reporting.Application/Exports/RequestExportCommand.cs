using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Exports;

/// <summary>
/// Pide exportar un informe y **devuelve enseguida**.
///
/// Es la primera condición del plan: «el usuario pide la exportación y recupera el control
/// inmediatamente; no se queda mirando una barra». Este comando sólo deja la petición apuntada;
/// el fichero lo hace un trabajador en segundo plano.
///
/// Hacerlo aquí, en la petición HTTP, sería más corto de escribir y tendría dos problemas: un
/// informe grande agotaría el tiempo de espera del navegador, y no serviría para los informes
/// programados, que ocurren sin nadie delante.
/// </summary>
public sealed record RequestExportCommand(
    Guid TenantId,
    Guid ReportId,
    Guid RequestedById,
    string Format) : ICommand<ExportDto>;
