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

/// <summary>
/// La agenda de un día, que cruza cuatro módulos.
///
/// <b>Vive en el host, como <see cref="ApiHost.Reporting.ConsultasDelPanel"/> y por lo mismo:</b>
/// ningún módulo referencia a otro, así que lo que necesita datos de varios se compone aquí. Que
/// Calendar supiera de tareas o de tickets rompería el aislamiento por una pantalla.
///
/// Se sirve en una sola petición y no en cuatro. Quien abre un día quiere ver el día; cuatro
/// llamadas desde el navegador harían que las secciones fueran apareciendo de una en una, y con
/// una lenta el resto parecería incompleto.
/// </summary>
public sealed class DailyAgenda(
    IMediator mediator,
    IUserContext currentUser,
    ProjectsDbContext projectsDb,
    WorkItemsDbContext tasksDb,
    TicketingDbContext ticketsDb)
{
    public async Task<DailyAgendaDto> ForDayAsync(DateOnly day, CancellationToken ct = default)
    {
        var from = day.ToDateTime(TimeOnly.MinValue);
        var to = day.ToDateTime(TimeOnly.MaxValue);

        var events = await EventsBetweenAsync(from, to, ct);

        // Los tres en paralelo no: comparten el mismo `DbContext`… no, cada uno es el suyo, pero
        // van seguidos igualmente porque son tres consultas cortas contra la misma base y
        // lanzarlas a la vez sólo añade conexiones para ahorrar milisegundos.
        var tasks = await tasksDb.Tasks
            .AsNoTracking()
            .Where(t => t.TenantId == currentUser.TenantId && t.DueDate == day)
            .OrderBy(t => t.Title.Value)
            .Select(t => new AgendaItem(EntityTypes.Task, t.Id, t.Title.Value, t.Status.Name, null, null, false))
            .ToListAsync(ct);

        // «Tickets del día» son los que se abrieron ese día. Un ticket no tiene fecha de
        // vencimiento —sólo creación y resolución—, así que cualquier otra lectura sería
        // inventada. Si algún día tienen vencimiento, este es el sitio donde cambiarlo.
        var tickets = await ticketsDb.Tickets
            .AsNoTracking()
            .Where(t => t.TenantId == currentUser.TenantId && t.CreatedAt >= from && t.CreatedAt <= to)
            .OrderBy(t => t.CreatedAt)
            .Select(t => new AgendaItem(EntityTypes.Ticket, t.Id, t.Title, t.Description, t.CreatedAt, null, false))
            .ToListAsync(ct);

        var projects = await projectsDb.Projects
            .AsNoTracking()
            .Where(p => p.TenantId == currentUser.TenantId && p.EstimatedEndDate == day)
            .OrderBy(p => p.Name.Value)
            .Select(p => new AgendaItem(EntityTypes.Project, p.Id, p.Name.Value, p.Status.Name, null, null, false))
            .ToListAsync(ct);

        return new DailyAgendaDto(day, events, tasks, tickets, projects);
    }

    /// <summary>
    /// Los eventos del día, pasando por el módulo y no por su <c>DbContext</c>.
    ///
    /// Calendar tiene su consulta y su contrato; usarlos es lo correcto. A los otros tres se les
    /// entra por el contexto porque lo que se necesita —«las tareas que vencen el martes»— no
    /// existe en ninguna de sus consultas, y añadirlo a cada módulo para una pantalla del
    /// calendario les metería un concepto que no es suyo.
    /// </summary>
    private async Task<List<AgendaItem>> EventsBetweenAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var result = await mediator.Send(
            new GetEventsQuery(currentUser.TenantId, from, to, null, new() { Page = 1, PageSize = 500 }), ct);

        if (result.IsFailure || result.Value is null) return [];

        return [.. result.Value.Items
            .OrderBy(e => e.StartTime)
            .Select(e => new AgendaItem(
                "Event", e.Id, e.Title, e.Location ?? e.Description,
                e.StartTime, e.EndTime, e.CancelledAtUtc is not null))];
    }
}
