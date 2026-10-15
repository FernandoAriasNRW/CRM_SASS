using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;

namespace Reporting.Domain.ValueObjects;

public sealed class ReportName : ValueObject
{
  public string Value { get; }

  private ReportName() { Value = null!; } // EF las rellena al materializar.
  private ReportName(string value) => Value = value;

  /// <summary>
  /// El nombre, recortado. Los mensajes llegan tal cual a la pantalla, así que van en español; y
  /// se mide sin los espacios de los extremos: «  ab  » pasaba el mínimo de tres y se guardaba
  /// con ellos.
  /// </summary>
  public static Result<ReportName> Create(string? name)
  {
    var clean = (name ?? string.Empty).Trim();

    if (clean.Length == 0)
      return Result<ReportName>.Failure("El informe necesita un nombre");

    if (clean.Length < 3)
      return Result<ReportName>.Failure("El nombre del informe necesita al menos 3 caracteres");

    if (clean.Length > 100)
      return Result<ReportName>.Failure("El nombre del informe no puede pasar de 100 caracteres");

    return Result<ReportName>.Success(new ReportName(clean));
  }

  public override IEnumerable<object> GetEqualityComponents()
  {
    yield return Value;
  }
}

/// <summary>
/// Los tipos de informe que se pueden pedir.
///
/// **Esta lista y la del desplegable del frontend tienen que coincidir.** No coincidían:
/// la pantalla ofrecía «Distribución de Tareas» (TaskBreakdown) y «Resumen de KPIs»
/// (KpiSummary), que no estaban aquí, y pedir cualquiera de los dos devolvía 400 con el
/// mensaje «Invalid report type or format» —que además no decía cuál de los dos era el
/// inválido—. Dos de las cuatro opciones del formulario no funcionaban.
///
/// Se añaden en vez de quitarlos del desplegable porque el sistema **sí sabe calcular ambas
/// cosas**: son exactamente lo que devuelven `/api/v1/reports/tasks/breakdown` y
/// `/api/v1/reports/kpi`. Lo que faltaba era el nombre en esta lista.
///
/// `ReportContractTests` comprueba que todo lo que ofrece la pantalla se acepta aquí, para
/// que la próxima vez que se añada una opción no vuelva a fallar en silencio hasta que alguien
/// la pulse.
/// </summary>
public sealed class ReportType : Enumeration
{
  public static readonly ReportType TaskSummary = new(1, "TaskSummary");
  public static readonly ReportType ProjectProgress = new(2, "ProjectProgress");
  public static readonly ReportType UserActivity = new(3, "UserActivity");
  public static readonly ReportType TicketAnalytics = new(4, "TicketAnalytics");
  public static readonly ReportType Custom = new(5, "Custom");
  public static readonly ReportType TaskBreakdown = new(6, "TaskBreakdown");
  public static readonly ReportType KpiSummary = new(7, "KpiSummary");

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