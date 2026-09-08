using BuildingBlocks.Application.Abstractions;
using Calendar.Application.DTOs;
using Calendar.Application.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Projects.Infrastructure.Persistence;
using Ticketing.Infrastructure.Persistence;
using WorkItems.Infrastructure.Persistence;

namespace ApiHost.Calendar;

/// <summary>Una cosa que cae en un día, venga del módulo que venga.</summary>
/// <param name="Tipo">«Evento», «Tarea», «Ticket» o «Proyecto». Es lo que decide el icono y a
/// dónde lleva al pulsar.</param>
/// <param name="Hora">La hora, si la tiene. Las tareas y los proyectos vencen el día entero, así
/// que va en nulo y se enseñan arriba en vez de repartidas por horas inventadas.</param>
public sealed record CosaDelDia(
    string Tipo,
    Guid Id,
    string Titulo,
    string? Detalle,
    DateTime? Hora,
    DateTime? HoraFin,
    bool Anulado);

/// <summary>Todo lo que pasa un día: eventos, y lo que vence de los otros módulos.</summary>
public sealed record AgendaDeUnDia(
    DateOnly Dia,
    IReadOnlyList<CosaDelDia> Eventos,
    IReadOnlyList<CosaDelDia> TareasQueVencen,
    IReadOnlyList<CosaDelDia> TicketsDelDia,
    IReadOnlyList<CosaDelDia> ProyectosQueTerminan);

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
public sealed class AgendaDelDia(
    IMediator mediator,
    IUserContext usuario,
    ProjectsDbContext proyectosDb,
    WorkItemsDbContext tareasDb,
    TicketingDbContext ticketsDb)
{
    public async Task<AgendaDeUnDia> DeAsync(DateOnly dia, CancellationToken ct = default)
    {
        var desde = dia.ToDateTime(TimeOnly.MinValue);
        var hasta = dia.ToDateTime(TimeOnly.MaxValue);

        var eventos = await EventosDeAsync(desde, hasta, ct);

        // Los tres en paralelo no: comparten el mismo `DbContext`… no, cada uno es el suyo, pero
        // van seguidos igualmente porque son tres consultas cortas contra la misma base y
        // lanzarlas a la vez sólo añade conexiones para ahorrar milisegundos.
        var tareas = await tareasDb.Tasks
            .AsNoTracking()
            .Where(t => t.TenantId == usuario.TenantId && t.DueDate == dia)
            .OrderBy(t => t.Title.Value)
            .Select(t => new CosaDelDia("Tarea", t.Id, t.Title.Value, t.Status.Name, null, null, false))
            .ToListAsync(ct);

        // «Tickets del día» son los que se abrieron ese día. Un ticket no tiene fecha de
        // vencimiento —sólo creación y resolución—, así que cualquier otra lectura sería
        // inventada. Si algún día tienen vencimiento, este es el sitio donde cambiarlo.
        var tickets = await ticketsDb.Tickets
            .AsNoTracking()
            .Where(t => t.TenantId == usuario.TenantId && t.CreatedAt >= desde && t.CreatedAt <= hasta)
            .OrderBy(t => t.CreatedAt)
            .Select(t => new CosaDelDia("Ticket", t.Id, t.Title, t.Description, t.CreatedAt, null, false))
            .ToListAsync(ct);

        var proyectos = await proyectosDb.Projects
            .AsNoTracking()
            .Where(p => p.TenantId == usuario.TenantId && p.EstimatedEndDate == dia)
            .OrderBy(p => p.Name.Value)
            .Select(p => new CosaDelDia("Proyecto", p.Id, p.Name.Value, p.Status.Name, null, null, false))
            .ToListAsync(ct);

        return new AgendaDeUnDia(dia, eventos, tareas, tickets, proyectos);
    }

    /// <summary>
    /// Los eventos del día, pasando por el módulo y no por su <c>DbContext</c>.
    ///
    /// Calendar tiene su consulta y su contrato; usarlos es lo correcto. A los otros tres se les
    /// entra por el contexto porque lo que se necesita —«las tareas que vencen el martes»— no
    /// existe en ninguna de sus consultas, y añadirlo a cada módulo para una pantalla del
    /// calendario les metería un concepto que no es suyo.
    /// </summary>
    private async Task<List<CosaDelDia>> EventosDeAsync(DateTime desde, DateTime hasta, CancellationToken ct)
    {
        var resultado = await mediator.Send(
            new GetEventsQuery(usuario.TenantId, desde, hasta, null, new() { Page = 1, PageSize = 500 }), ct);

        if (resultado.IsFailure || resultado.Value is null) return [];

        return [.. resultado.Value.Items
            .OrderBy(e => e.StartTime)
            .Select(e => new CosaDelDia(
                "Evento", e.Id, e.Title, e.Location ?? e.Description,
                e.StartTime, e.EndTime, e.CanceladoEnUtc is not null))];
    }
}
