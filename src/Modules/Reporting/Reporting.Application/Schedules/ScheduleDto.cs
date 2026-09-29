using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Schedules;

public sealed record ScheduleDto(
    Guid Id,
    Guid ReportId,
    string Frequency,
    string Format,
    string Time,
    int? Day,
    bool IsActive,
    DateOnly? LastGeneratedDay);
