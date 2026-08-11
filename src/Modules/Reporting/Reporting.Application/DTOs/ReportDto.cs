using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.DTOs;

public class ReportDto(Guid Id, Guid tenantId, Guid createdById, string name, string type, string format, string? parameters = null)
{
  public Guid Id { get; set; } = Id;
  public Guid TenantId { get; set; } = tenantId;
  public Guid CreatedById { get; set; } = createdById;
  public string Name { get; set; } = name;
  public string Type { get; set; } = type;
  public string Format { get; set; } = format;
  public string? Parameters { get; set; } = parameters;
  public string? GeneratedFileUrl { get; set; } = null;
  public DateTime? GeneratedAt { get; set; } = DateTime.Now;
  public string? ErrorMessage { get; set; } = null;
  public bool IsGenerated { get; set; } = false;

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