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

/// <summary>Una cosa que cae en un día, venga del módulo que venga.</summary>
/// <param name="Type">«Event», o uno de <see cref="EntityTypes"/> («Task», «Ticket», «Project»). Es lo que decide el icono y a
/// dónde lleva al pulsar.</param>
/// <param name="Time">La hora, si la tiene. Las tareas y los proyectos vencen el día entero, así
/// que va en nulo y se enseñan arriba en vez de repartidas por horas inventadas.</param>
public sealed record AgendaItem(
    string Type,
    Guid Id,
    string Title,
    string? Detail,
    DateTime? Time,
    DateTime? EndTime,
    bool IsCancelled);
