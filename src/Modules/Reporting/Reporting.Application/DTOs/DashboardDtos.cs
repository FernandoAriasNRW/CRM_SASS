namespace Reporting.Application.DTOs;

public record KpiDataDto(
    int TotalProjects,
    int TotalTasks,
    int DoneTasks,
    double Throughput,
    int OpenTickets,
    int InProgressTickets,
    double AvgLeadTimeDays,
    double AvgCycleTimeDays
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
