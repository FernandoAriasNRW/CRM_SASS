using BuildingBlocks.Domain.Primitives;

namespace Reporting.Domain.Events;

/// <summary>
/// Una exportación falló, con su motivo.
///
/// Se avisa igual que del éxito, y por la misma razón: quien pidió un informe y no recibe nada
/// no sabe si esperar más. Un fallo callado obliga a preguntar.
/// </summary>
public sealed record ExportFailedEvent(
    Guid ExportId, Guid TenantId, Guid ReportId, Guid RequestedById, string Error) : DomainEvent;
