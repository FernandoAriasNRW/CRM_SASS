using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.DTOs;

/// <summary>
/// El informe tal como sale de la API: qué se quiere ver, no una copia concreta de ello.
///
/// **Ya no lleva estado de generación**, y conviene saber por qué, porque estos cuatro campos
/// —`GeneratedFileUrl`, `GeneratedAt`, `ErrorMessage` e `IsGenerated`— dieron dos fallos
/// seguidos:
///
///  - Primero, `FromEntity` **no los copiaba**: se quedaban en el valor de su inicializador. Un
///    informe generado salía siempre como no generado, sin URL, y con `GeneratedAt` a
///    `DateTime.Now` por defecto, o sea con una fecha de generación **inventada y distinta en
///    cada llamada**. Se arregló copiándolos.
///  - Y entonces se vio el fallo de debajo: lo que copiaban tampoco era cierto. La URL la
///    fabricaba `MarkAsGenerated` a mano y no apuntaba a ningún fichero.
///
/// Ahora el estado vive en `Exportacion`, una por petición y por formato, y se consulta en
/// `/api/v1/reports/{id}/exportaciones`. Un informe puede tener muchas exportaciones —o ninguna—
/// y ese «muchas» es justo lo que cuatro campos sueltos no sabían representar.
/// </summary>
public class ReportDto(Guid Id, Guid tenantId, Guid createdById, string name, string type, string format, string? parameters = null)
{
  public Guid Id { get; set; } = Id;
  public Guid TenantId { get; set; } = tenantId;
  public Guid CreatedById { get; set; } = createdById;
  public string Name { get; set; } = name;
  public string Type { get; set; } = type;
  public string Format { get; set; } = format;
  public string? Parameters { get; set; } = parameters;

  public static ReportDto FromEntity(Report report)
  {
    return new ReportDto(
        report.Id,
        report.TenantId,
        report.CreatedById,
        report.Name,
        report.Type.Name,
        report.Format.Name,
        report.Parameters
    );
  }

  public Report ToEntity()
  {
    try
    {
      var report = Report.Create(TenantId, CreatedById, Name, ReportType.FromName<ReportType>(Type)!, ReportFormat.FromName<ReportFormat>(Format)!);

      if (report.IsFailure)
      {
        throw new InvalidOperationException($"Failed to create Report entity: {report.Error}");
      }

      if (report.Value is null)
      {
        throw new InvalidOperationException("Report creation resulted in a null value.");
      }

      return report.Value;
    }
    catch (Exception ex)
    {
      throw new InvalidOperationException($"Error converting ReportDto to Report entity: {ex.Message}", ex);
    }
  }
}