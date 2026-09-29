using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Schedules;

/// <summary>
/// Programa un informe: cada cuánto, a qué hora y en qué formato.
///
/// La hora llega como «HH:mm» y es **local del inquilino**, no UTC. Ver
/// <see cref="ReportSchedule"/> para el porqué.
/// </summary>
public sealed record ScheduleReportCommand(
    Guid TenantId,
    Guid ReportId,
    Guid RecipientId,
    string Frequency,
    string Format,
    string Time,
    int? Day) : ICommand<ScheduleDto>;
