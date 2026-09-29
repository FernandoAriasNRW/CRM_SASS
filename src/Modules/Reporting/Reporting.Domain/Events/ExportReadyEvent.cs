using BuildingBlocks.Domain.Primitives;

namespace Reporting.Domain.Events;

/// <summary>
/// Una exportación terminó y hay fichero.
///
/// Lo escucha el host para avisar a quien la pidió, respetando sus preferencias. Va por evento y
/// no llamando a Notifications desde aquí porque Reporting no conoce a Notifications: ningún
/// módulo referencia a otro, y el host es quien los conoce a los dos.
/// </summary>
public sealed record ExportReadyEvent(
    Guid ExportId, Guid TenantId, Guid ReportId, Guid RequestedById, string FileName) : DomainEvent;
