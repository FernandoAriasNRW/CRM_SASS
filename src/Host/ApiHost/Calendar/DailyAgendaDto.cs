using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Calendar.Application.DTOs;
using Calendar.Application.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Projects.Infrastructure.Persistence;
using Ticketing.Infrastructure.Persistence;
using WorkItems.Infrastructure.Persistence;

namespace ApiHost.Calendar;

/// <summary>Todo lo que pasa un día: eventos, y lo que vence de los otros módulos.</summary>
public sealed record DailyAgendaDto(
    DateOnly Day,
    IReadOnlyList<AgendaItem> Events,
    IReadOnlyList<AgendaItem> TasksDue,
    IReadOnlyList<AgendaItem> TicketsOpened,
    IReadOnlyList<AgendaItem> ProjectsEnding);
