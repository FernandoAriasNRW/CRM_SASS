using BuildingBlocks.Domain.Primitives;

namespace Reporting.Domain.Events;

public sealed record ReportCreatedEvent(Guid ReportId, Guid TenantId, Guid CreatedById) : DomainEvent;
public sealed record ReportGeneratedEvent(Guid ReportId, Guid TenantId, string FileUrl) : DomainEvent;
public sealed record ReportGenerationFailedEvent(Guid ReportId, Guid TenantId, string Error) : DomainEvent;


/// <summary>
/// Una exportación terminó y hay fichero.
///
/// Lo escucha el host para avisar a quien la pidió, respetando sus preferencias. Va por evento y
/// no llamando a Notifications desde aquí porque Reporting no conoce a Notifications: ningún
/// módulo referencia a otro, y el host es quien los conoce a los dos.
/// </summary>
public sealed record ExportacionListaEvent(
    Guid ExportacionId, Guid TenantId, Guid ReportId, Guid SolicitadaPorId, string NombreDeFichero) : DomainEvent;

/// <summary>
/// Una exportación falló, con su motivo.
///
/// Se avisa igual que del éxito, y por la misma razón: quien pidió un informe y no recibe nada
/// no sabe si esperar más. Un fallo callado obliga a preguntar.
/// </summary>
public sealed record ExportacionFallidaEvent(
    Guid ExportacionId, Guid TenantId, Guid ReportId, Guid SolicitadaPorId, string Error) : DomainEvent;
