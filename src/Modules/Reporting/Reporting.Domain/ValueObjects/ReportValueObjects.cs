using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;

namespace Reporting.Domain.ValueObjects;

public sealed class ReportName : ValueObject
{
  public string Value { get; }

  private ReportName() { }
  private ReportName(string value) => Value = value;

  public static Result<ReportName> Create(string name)
  {
    if (string.IsNullOrWhiteSpace(name))
      return Result<ReportName>.Failure("Report name is required");

    if (name.Length < 3)
      return Result<ReportName>.Failure("Report name must be at least 3 characters");

    if (name.Length > 100)
      return Result<ReportName>.Failure("Report name must not exceed 100 characters");

    return Result<ReportName>.Success(new ReportName(name));
  }

  public override IEnumerable<object> GetEqualityComponents()
  {
    yield return Value;
  }
}

public sealed class ReportType : Enumeration
{
  public static readonly ReportType TaskSummary = new(1, "TaskSummary");
  public static readonly ReportType ProjectProgress = new(2, "ProjectProgress");
  public static readonly ReportType UserActivity = new(3, "UserActivity");
  public static readonly ReportType TicketAnalytics = new(4, "TicketAnalytics");
  public static readonly ReportType Custom = new(5, "Custom");

  private ReportType() : base(0, string.Empty) { }
  private ReportType(int value, string name) : base(value, name)
  {
  }

  public static IReadOnlyList<ReportType> All() => GetAll<ReportType>();
}

public sealed class ReportFormat : Enumeration
{
  public static readonly ReportFormat Pdf = new(1, "Pdf");
  public static readonly ReportFormat Excel = new(2, "Excel");
  public static readonly ReportFormat Csv = new(3, "Csv");

  private ReportFormat() : base(0, string.Empty) { }
  private ReportFormat(int value, string name) : base(value, name)
  {
  }

  public static IReadOnlyList<ReportFormat> All() => GetAll<ReportFormat>();
}