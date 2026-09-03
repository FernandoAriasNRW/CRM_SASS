namespace Reporting.Application.DTOs;

/// <summary>
/// Las cifras de cabecera del panel.
///
/// Los dos tiempos son <c>double?</c> a propósito. Estaban escritos a mano —2,5 y 1,4 días,
/// fijos en el código— porque no había con qué calcularlos: <c>WorkTask</c> no guardaba
/// ninguna marca de tiempo. Ahora guarda creación y cierre, así que el tiempo de entrega sale
/// de los datos.
///
/// El de ciclo sigue sin poder calcularse y por eso viaja como <c>null</c>: mide desde que el
/// trabajo *empieza de verdad*, y eso exige saber cuándo entró en «En Progreso». No hay
/// historial de cambios de estado, sólo el estado actual. **Un hueco visible es mejor que un
/// número inventado**: quien lee el panel puede desconfiar de lo que no está, pero no de lo
/// que parece medido y no lo está.
/// </summary>
public record KpiDataDto(
    int TotalProjects,
    int TotalTasks,
    int DoneTasks,
    double Throughput,
    int OpenTickets,
    int InProgressTickets,
    double? AvgLeadTimeDays,
    double? AvgCycleTimeDays
);

public record TaskStatusBreakdownDto(
    string Status,
    int Count,
    string Color
);

public record ProjectProgressDto(
    Guid Id,
    string Name,
    string Status,
    int TotalTasks,
    int DoneTasks,
    double CompletionPct
);

public record ProjectBurndownDto(
    Guid ProjectId,
    string ProjectName,
    List<BurndownDataPointDto> Data
);

public record BurndownDataPointDto(
    string Date,
    int RemainingTasks,
    int IdealTasks
);
