using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.DTOs;

/// <summary>
/// El informe tal como sale de la API.
///
/// **Ojo con el estado de generación.** `FromEntity` copiaba siete campos y se dejaba cuatro
/// —`GeneratedFileUrl`, `GeneratedAt`, `ErrorMessage` e `IsGenerated`—, que se quedaban en el
/// valor de su inicializador. Consecuencias, y las tres se veían en la pantalla:
///
///  - `IsGenerated` salía **siempre false**, aunque la base dijera que sí. Un informe generado
///    no podía aparecer como generado nunca.
///  - `GeneratedFileUrl` salía **siempre null**, así que no había forma de descargarlo.
///  - `GeneratedAt` tenía por defecto `DateTime.Now`, de modo que la API **inventaba una fecha
///    de generación** —la de la propia petición— para informes que no se habían generado. Era
///    lo peor de los tres: los otros dos se notan porque falta algo; éste devolvía un dato
///    plausible y falso, distinto en cada llamada.
///
/// Ahora se copian los once. Los inicializadores se quitan para que el compilador avise si
/// mañana se añade un campo y alguien se olvida de mapearlo, en vez de rellenarlo en silencio.
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
  public string? GeneratedFileUrl { get; set; }
  public DateTime? GeneratedAt { get; set; }
  public string? ErrorMessage { get; set; }
  public bool IsGenerated { get; set; }

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
    )
    {
      GeneratedFileUrl = report.GeneratedFileUrl,
      GeneratedAt = report.GeneratedAt,
      ErrorMessage = report.ErrorMessage,
      IsGenerated = report.IsGenerated,
    };
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