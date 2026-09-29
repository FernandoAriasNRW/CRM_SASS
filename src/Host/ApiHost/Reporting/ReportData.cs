using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Projects.Infrastructure.Persistence;
using Reporting.Application.Exports;
using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;
using Ticketing.Infrastructure.Persistence;
using WorkItems.Infrastructure.Persistence;

namespace ApiHost.Reporting;

/// <summary>
/// Resuelve un informe a la tabla que se va a exportar.
///
/// <b>Vive en el host por la misma razón que <see cref="DashboardQueries"/>:</b> un informe de
/// tareas mira WorkItems, uno de tickets mira Ticketing, y ninguno de los dos módulos referencia
/// al otro ni a Reporting. El host es quien los conoce a todos.
///
/// <b>Es el mismo motor que pinta el informe en pantalla</b>, que era la advertencia del plan:
/// «con el mismo motor de consulta que pinta el reporte en pantalla». Los agregados —KPIs,
/// desglose, avance— salen de <see cref="DashboardQueries"/>, exactamente los mismos números
/// que ve el panel. Si se hubieran reescrito aquí, el PDF y la pantalla acabarían discrepando y
/// nadie sabría cuál creer.
///
/// El detalle fila a fila —una tarea por línea, un ticket por línea— sí se consulta aquí: el
/// panel no lo necesita porque no lo enseña, y un informe exportado sin detalle es una foto de
/// unos totales que ya se veían.
/// </summary>
public sealed class ReportData(
    DashboardQueries panel,
    ReportEngine motor,
    ProjectsDbContext projectsDb,
    WorkItemsDbContext tasksDb,
    TicketingDbContext ticketsDb)
{
    /// <summary>
    /// Cuántas filas de detalle entran como mucho en un informe.
    ///
    /// Hay tope porque el fichero se guarda entero en memoria antes de escribirse, y porque un
    /// PDF de cien mil filas no lo lee nadie. Cuando se alcanza, el subtítulo lo dice: un
    /// informe recortado en silencio es peor que uno corto, porque quien lo lea creerá que ésos
    /// son todos los datos.
    /// </summary>
    public const int MaxRows = 20_000;

    /// <summary>La cultura en que se formatean fechas y números del informe.</summary>
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    public async Task<ReportTable> ResolveAsync(Report report, CancellationToken ct)
    {
        var tenantId = report.TenantId;

        // El inquilino se declara en los tres contextos antes de consultar nada.
        //
        // **Es imprescindible cuando esto lo llama el trabajador de segundo plano**, que no tiene
        // petición y por tanto no tiene usuario: sin declararlo, el filtro global compara contra
        // Guid.Empty y devuelve cero filas de todo. El fichero salía bien formado, con su aviso,
        // y con ceros donde la API enseñaba 15 tareas y 215 tickets. Nada fallaba.
        //
        // Desde una petición HTTP es redundante —el inquilino ya es ése— y no molesta: se pone el
        // mismo valor que ya había.
        using var _ = tasksDb.AsTenant(tenantId);
        using var __ = ticketsDb.AsTenant(tenantId);
        using var ___ = projectsDb.AsTenant(tenantId);

        // El nombre que le puso quien lo creó encabeza el documento; el tipo decide qué datos
        // trae. Son dos cosas distintas y conviene no mezclarlas: dos informes del mismo tipo
        // pueden llamarse distinto porque filtran distinto.
        return report.Type.Name switch
        {
            nameof(ReportType.KpiSummary) => await KpisAsync(report, tenantId, ct),
            nameof(ReportType.TaskBreakdown) => await TaskBreakdownAsync(report, tenantId, ct),
            nameof(ReportType.ProjectProgress) => await ProjectProgressAsync(report, tenantId, ct),
            nameof(ReportType.TaskSummary) => await TasksAsync(report, tenantId, ct),
            nameof(ReportType.TicketAnalytics) => await TicketsAsync(report, tenantId, ct),
            nameof(ReportType.UserActivity) => await ActivityByPersonAsync(report, tenantId, ct),

            // Los informes a medida los resuelve el motor a partir de su definición guardada.
            nameof(ReportType.Custom) => await CustomAsync(report, ct),

            _ => throw new InvalidOperationException(
                $"El tipo de informe «{report.Type.Name}» no sabe generar datos todavía")
        };
    }

    /// <summary>
    /// Un informe a medida: el motor lo resuelve desde su definición.
    ///
    /// Si no tiene definición se dice, en vez de generar un fichero con encabezados y nada
    /// dentro: un informe vacío parece un fallo del sistema, y esto es un informe a medio
    /// configurar.
    /// </summary>
    private async Task<ReportTable> CustomAsync(Report report, CancellationToken ct)
    {
        var definition = report.ReadDefinition();

        if (definition is null)
        {
            throw new InvalidOperationException(
                "Este informe está marcado como personalizado pero no tiene definición. "
                + "Ábrelo en el constructor y elige el origen, la agrupación y la medida.");
        }

        return await motor.ResolveAsync(report.Name, report.TenantId, definition, ct);
    }

    private async Task<ReportTable> KpisAsync(Report report, Guid tenantId, CancellationToken ct)
    {
        var kpi = await panel.GetKpiDataAsync(tenantId, ct);

        // Un KPI por fila, no una fila con todas las columnas. Así el informe se lee de arriba
        // abajo en cualquier formato y añadir un indicador no descuadra la tabla.
        // Expresión de colección y no inicializador con llaves: `{ ["a", "b"] }` C# lo lee como
        // un inicializador de indexador, no como una lista de listas.
        List<IReadOnlyList<string>> rows =
        [
            ["Proyectos", kpi.TotalProjects.ToString(Spanish)],
            ["Tareas", kpi.TotalTasks.ToString(Spanish)],
            ["Tareas terminadas", kpi.DoneTasks.ToString(Spanish)],
            ["Porcentaje completado", kpi.Throughput.ToString("0.#", Spanish) + " %"],
            ["Tickets abiertos", kpi.OpenTickets.ToString(Spanish)],
            ["Tickets en curso", kpi.InProgressTickets.ToString(Spanish)],

            // Los huecos se escriben como raya y no como cero. Un cero aquí diría «se entrega en
            // el acto», que es lo contrario de «todavía no se puede calcular».
            ["Tiempo medio de entrega (días)", FormatNumber(kpi.AvgLeadTimeDays)],
            ["Tiempo medio de ciclo (días)", FormatNumber(kpi.AvgCycleTimeDays)]
        ];

        return new ReportTable(report.Name, Subtitle(rows.Count), ["Indicador", "Valor"], rows);
    }

    private async Task<ReportTable> TaskBreakdownAsync(Report report, Guid tenantId, CancellationToken ct)
    {
        var breakdown = await panel.GetTaskBreakdownAsync(tenantId, ct);

        var rows = breakdown
            .Select(d => (IReadOnlyList<string>)[d.Status, d.Count.ToString(Spanish)])
            .ToList();

        return new ReportTable(report.Name, Subtitle(rows.Count), ["Estado", "Tareas"], rows);
    }

    private async Task<ReportTable> ProjectProgressAsync(Report report, Guid tenantId, CancellationToken ct)
    {
        var progress = await panel.GetProjectProgressAsync(tenantId, ct);

        var rows = progress
            .Select(p => (IReadOnlyList<string>)
            [
                p.Name,
                p.Status,
                p.TotalTasks.ToString(Spanish),
                p.DoneTasks.ToString(Spanish),
                p.CompletionPct.ToString("0.#", Spanish) + " %"
            ])
            .ToList();

        return new ReportTable(
            report.Name, Subtitle(rows.Count),
            ["Proyecto", "Estado", "Tareas", "Terminadas", "Avance"], rows);
    }

    private async Task<ReportTable> TasksAsync(Report report, Guid tenantId, CancellationToken ct)
    {
        // El detalle respeta los filtros globales: nada archivado ni en la papelera sale en un
        // informe. Es la ventaja de haberlos puesto en el filtro global y no consulta a
        // consulta —esta consulta se escribió después y los hereda sin saberlo—.
        var tasks = await tasksDb.Tasks.AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .OrderByDescending(t => t.DueDate)
            .Take(MaxRows)
            .Select(t => new
            {
                Titulo = t.Title.Value,
                Estado = t.Status.Value,
                Prioridad = t.Priority.Value,
                t.DueDate,
                t.EstimatedHours,
                Responsables = t.Assignees.Count
            })
            .ToListAsync(ct);

        var rows = tasks
            .Select(t => (IReadOnlyList<string>)
            [
                t.Titulo,
                t.Estado,
                t.Prioridad,
                t.DueDate.ToString("dd/MM/yyyy", Spanish),
                t.EstimatedHours.ToString("0.##", Spanish),
                t.Responsables.ToString(Spanish)
            ])
            .ToList();

        return new ReportTable(
            report.Name, Subtitle(rows.Count),
            ["Tarea", "Estado", "Prioridad", "Vence", "Horas estimadas", "Responsables"], rows);
    }

    private async Task<ReportTable> TicketsAsync(Report report, Guid tenantId, CancellationToken ct)
    {
        var tickets = await ticketsDb.Tickets.AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(MaxRows)
            .Select(t => new { t.Title, t.PriorityValue, t.StatusValue, t.CreatedAt, t.ResolvedAt })
            .ToListAsync(ct);

        var rows = tickets
            .Select(t => (IReadOnlyList<string>)
            [
                t.Title,
                Ticketing.Domain.ValueObjects.TicketPriority.FromValue<Ticketing.Domain.ValueObjects.TicketPriority>(t.PriorityValue).Name,
                Ticketing.Domain.ValueObjects.TicketStatus.FromValue<Ticketing.Domain.ValueObjects.TicketStatus>(t.StatusValue).Name,
                t.CreatedAt.ToString("dd/MM/yyyy", Spanish),
                t.ResolvedAt?.ToString("dd/MM/yyyy", Spanish) ?? "—",

                // Los días que lleva abierto, o los que tardó. Es la columna por la que se ordena
                // cuando alguien busca qué se está atascando, y calcularla aquí evita que cada
                // quien la saque a mano en una hoja de cálculo.
                ((t.ResolvedAt ?? DateTime.UtcNow) - t.CreatedAt).TotalDays.ToString("0.#", Spanish)
            ])
            .ToList();

        return new ReportTable(
            report.Name, Subtitle(rows.Count),
            ["Ticket", "Prioridad", "Estado", "Creado", "Resuelto", "Días"], rows);
    }

    private async Task<ReportTable> ActivityByPersonAsync(Report report, Guid tenantId, CancellationToken ct)
    {
        // Se agrupa por responsable principal. Las tareas con varios responsables cuentan en el
        // principal y no en todos, para que la suma de la columna sea el total de tareas: un
        // informe cuyas partes suman más que el total es un informe que nadie usa dos veces.
        var byPerson = await tasksDb.Tasks.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.AssigneeId != Guid.Empty)
            .GroupBy(t => t.AssigneeId)
            .Select(g => new
            {
                Persona = g.Key,
                Total = g.Count(),
                Terminadas = g.Count(t => t.Status.Value == "Done"),
                Horas = g.Sum(t => t.EstimatedHours)
            })
            .OrderByDescending(x => x.Total)
            .Take(MaxRows)
            .ToListAsync(ct);

        // El identificador y no el nombre: los nombres viven en Identity, y este informe ya cruza
        // dos módulos. Ponerle nombre exige un puerto nuevo, y es trabajo aparte —queda anotado
        // en la auditoría—.
        var rows = byPerson
            .Select(p => (IReadOnlyList<string>)
            [
                p.Persona.ToString(),
                p.Total.ToString(Spanish),
                p.Terminadas.ToString(Spanish),
                p.Horas.ToString("0.##", Spanish)
            ])
            .ToList();

        return new ReportTable(
            report.Name, Subtitle(rows.Count),
            ["Persona", "Tareas", "Terminadas", "Horas estimadas"], rows);
    }

    private static string FormatNumber(double? value)
        => value?.ToString("0.#", Spanish) ?? "—";

    /// <summary>
    /// La línea de contexto: cuándo se generó y cuántas filas trae.
    ///
    /// Avisa cuando el informe llegó al tope. Recortar sin decirlo es la peor opción: quien lea
    /// el fichero dará por hecho que ésos son todos los datos.
    /// </summary>
    private static string Subtitle(int rows)
    {
        var generated = $"Generado el {DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm", Spanish)} UTC";

        return rows >= MaxRows
            ? $"{generated} · {rows:N0} filas (recortado: el informe tiene más datos de los que caben)"
            : $"{generated} · {rows:N0} filas";
    }
}
