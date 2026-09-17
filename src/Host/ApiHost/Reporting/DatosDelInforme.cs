using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Projects.Infrastructure.Persistence;
using Reporting.Application.Exportaciones;
using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;
using Ticketing.Infrastructure.Persistence;
using WorkItems.Infrastructure.Persistence;

namespace ApiHost.Reporting;

/// <summary>
/// Resuelve un informe a la tabla que se va a exportar.
///
/// <b>Vive en el host por la misma razón que <see cref="ConsultasDelPanel"/>:</b> un informe de
/// tareas mira WorkItems, uno de tickets mira Ticketing, y ninguno de los dos módulos referencia
/// al otro ni a Reporting. El host es quien los conoce a todos.
///
/// <b>Es el mismo motor que pinta el informe en pantalla</b>, que era la advertencia del plan:
/// «con el mismo motor de consulta que pinta el reporte en pantalla». Los agregados —KPIs,
/// desglose, avance— salen de <see cref="ConsultasDelPanel"/>, exactamente los mismos números
/// que ve el panel. Si se hubieran reescrito aquí, el PDF y la pantalla acabarían discrepando y
/// nadie sabría cuál creer.
///
/// El detalle fila a fila —una tarea por línea, un ticket por línea— sí se consulta aquí: el
/// panel no lo necesita porque no lo enseña, y un informe exportado sin detalle es una foto de
/// unos totales que ya se veían.
/// </summary>
public sealed class DatosDelInforme(
    ConsultasDelPanel panel,
    MotorDeInformes motor,
    ProjectsDbContext proyectosDb,
    WorkItemsDbContext tareasDb,
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
    public const int FilasMaximas = 20_000;

    /// <summary>La cultura en que se formatean fechas y números del informe.</summary>
    private static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-ES");

    public async Task<TablaDeInforme> ResolveAsync(Report informe, CancellationToken ct)
    {
        var tenantId = informe.TenantId;

        // El inquilino se declara en los tres contextos antes de consultar nada.
        //
        // **Es imprescindible cuando esto lo llama el trabajador de segundo plano**, que no tiene
        // petición y por tanto no tiene usuario: sin declararlo, el filtro global compara contra
        // Guid.Empty y devuelve cero filas de todo. El fichero salía bien formado, con su aviso,
        // y con ceros donde la API enseñaba 15 tareas y 215 tickets. Nada fallaba.
        //
        // Desde una petición HTTP es redundante —el inquilino ya es ése— y no molesta: se pone el
        // mismo valor que ya había.
        using var _ = tareasDb.AsTenant(tenantId);
        using var __ = ticketsDb.AsTenant(tenantId);
        using var ___ = proyectosDb.AsTenant(tenantId);

        // El nombre que le puso quien lo creó encabeza el documento; el tipo decide qué datos
        // trae. Son dos cosas distintas y conviene no mezclarlas: dos informes del mismo tipo
        // pueden llamarse distinto porque filtran distinto.
        return informe.Type.Name switch
        {
            nameof(ReportType.KpiSummary) => await KpisAsync(informe, tenantId, ct),
            nameof(ReportType.TaskBreakdown) => await DesgloseDeTareasAsync(informe, tenantId, ct),
            nameof(ReportType.ProjectProgress) => await AvanceDeProyectosAsync(informe, tenantId, ct),
            nameof(ReportType.TaskSummary) => await TareasAsync(informe, tenantId, ct),
            nameof(ReportType.TicketAnalytics) => await TicketsAsync(informe, tenantId, ct),
            nameof(ReportType.UserActivity) => await ActividadPorPersonaAsync(informe, tenantId, ct),

            // Los informes a medida los resuelve el motor a partir de su definición guardada.
            nameof(ReportType.Custom) => await AMedidaAsync(informe, ct),

            _ => throw new InvalidOperationException(
                $"El tipo de informe «{informe.Type.Name}» no sabe generar datos todavía")
        };
    }

    /// <summary>
    /// Un informe a medida: el motor lo resuelve desde su definición.
    ///
    /// Si no tiene definición se dice, en vez de generar un fichero con encabezados y nada
    /// dentro: un informe vacío parece un fallo del sistema, y esto es un informe a medio
    /// configurar.
    /// </summary>
    private async Task<TablaDeInforme> AMedidaAsync(Report informe, CancellationToken ct)
    {
        var definicion = informe.LeerDefinicion();

        if (definicion is null)
        {
            throw new InvalidOperationException(
                "Este informe está marcado como personalizado pero no tiene definición. "
                + "Ábrelo en el constructor y elige el origen, la agrupación y la medida.");
        }

        return await motor.ResolveAsync(informe.Name, informe.TenantId, definicion, ct);
    }

    private async Task<TablaDeInforme> KpisAsync(Report informe, Guid tenantId, CancellationToken ct)
    {
        var kpi = await panel.GetKpiDataAsync(tenantId, ct);

        // Un KPI por fila, no una fila con todas las columnas. Así el informe se lee de arriba
        // abajo en cualquier formato y añadir un indicador no descuadra la tabla.
        // Expresión de colección y no inicializador con llaves: `{ ["a", "b"] }` C# lo lee como
        // un inicializador de indexador, no como una lista de listas.
        List<IReadOnlyList<string>> filas =
        [
            ["Proyectos", kpi.TotalProjects.ToString(Espanol)],
            ["Tareas", kpi.TotalTasks.ToString(Espanol)],
            ["Tareas terminadas", kpi.DoneTasks.ToString(Espanol)],
            ["Porcentaje completado", kpi.Throughput.ToString("0.#", Espanol) + " %"],
            ["Tickets abiertos", kpi.OpenTickets.ToString(Espanol)],
            ["Tickets en curso", kpi.InProgressTickets.ToString(Espanol)],

            // Los huecos se escriben como raya y no como cero. Un cero aquí diría «se entrega en
            // el acto», que es lo contrario de «todavía no se puede calcular».
            ["Tiempo medio de entrega (días)", Numero(kpi.AvgLeadTimeDays)],
            ["Tiempo medio de ciclo (días)", Numero(kpi.AvgCycleTimeDays)]
        ];

        return new TablaDeInforme(informe.Name, Subtitulo(filas.Count), ["Indicador", "Valor"], filas);
    }

    private async Task<TablaDeInforme> DesgloseDeTareasAsync(Report informe, Guid tenantId, CancellationToken ct)
    {
        var desglose = await panel.GetTaskBreakdownAsync(tenantId, ct);

        var filas = desglose
            .Select(d => (IReadOnlyList<string>)[d.Status, d.Count.ToString(Espanol)])
            .ToList();

        return new TablaDeInforme(informe.Name, Subtitulo(filas.Count), ["Estado", "Tareas"], filas);
    }

    private async Task<TablaDeInforme> AvanceDeProyectosAsync(Report informe, Guid tenantId, CancellationToken ct)
    {
        var avance = await panel.GetProjectProgressAsync(tenantId, ct);

        var filas = avance
            .Select(p => (IReadOnlyList<string>)
            [
                p.Name,
                p.Status,
                p.TotalTasks.ToString(Espanol),
                p.DoneTasks.ToString(Espanol),
                p.CompletionPct.ToString("0.#", Espanol) + " %"
            ])
            .ToList();

        return new TablaDeInforme(
            informe.Name, Subtitulo(filas.Count),
            ["Proyecto", "Estado", "Tareas", "Terminadas", "Avance"], filas);
    }

    private async Task<TablaDeInforme> TareasAsync(Report informe, Guid tenantId, CancellationToken ct)
    {
        // El detalle respeta los filtros globales: nada archivado ni en la papelera sale en un
        // informe. Es la ventaja de haberlos puesto en el filtro global y no consulta a
        // consulta —esta consulta se escribió después y los hereda sin saberlo—.
        var tareas = await tareasDb.Tasks.AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .OrderByDescending(t => t.DueDate)
            .Take(FilasMaximas)
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

        var filas = tareas
            .Select(t => (IReadOnlyList<string>)
            [
                t.Titulo,
                t.Estado,
                t.Prioridad,
                t.DueDate.ToString("dd/MM/yyyy", Espanol),
                t.EstimatedHours.ToString("0.##", Espanol),
                t.Responsables.ToString(Espanol)
            ])
            .ToList();

        return new TablaDeInforme(
            informe.Name, Subtitulo(filas.Count),
            ["Tarea", "Estado", "Prioridad", "Vence", "Horas estimadas", "Responsables"], filas);
    }

    private async Task<TablaDeInforme> TicketsAsync(Report informe, Guid tenantId, CancellationToken ct)
    {
        var tickets = await ticketsDb.Tickets.AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(FilasMaximas)
            .Select(t => new { t.Title, t.PriorityValue, t.StatusValue, t.CreatedAt, t.ResolvedAt })
            .ToListAsync(ct);

        var filas = tickets
            .Select(t => (IReadOnlyList<string>)
            [
                t.Title,
                Ticketing.Domain.ValueObjects.TicketPriority.FromValue<Ticketing.Domain.ValueObjects.TicketPriority>(t.PriorityValue).Name,
                Ticketing.Domain.ValueObjects.TicketStatus.FromValue<Ticketing.Domain.ValueObjects.TicketStatus>(t.StatusValue).Name,
                t.CreatedAt.ToString("dd/MM/yyyy", Espanol),
                t.ResolvedAt?.ToString("dd/MM/yyyy", Espanol) ?? "—",

                // Los días que lleva abierto, o los que tardó. Es la columna por la que se ordena
                // cuando alguien busca qué se está atascando, y calcularla aquí evita que cada
                // quien la saque a mano en una hoja de cálculo.
                ((t.ResolvedAt ?? DateTime.UtcNow) - t.CreatedAt).TotalDays.ToString("0.#", Espanol)
            ])
            .ToList();

        return new TablaDeInforme(
            informe.Name, Subtitulo(filas.Count),
            ["Ticket", "Prioridad", "Estado", "Creado", "Resuelto", "Días"], filas);
    }

    private async Task<TablaDeInforme> ActividadPorPersonaAsync(Report informe, Guid tenantId, CancellationToken ct)
    {
        // Se agrupa por responsable principal. Las tareas con varios responsables cuentan en el
        // principal y no en todos, para que la suma de la columna sea el total de tareas: un
        // informe cuyas partes suman más que el total es un informe que nadie usa dos veces.
        var porPersona = await tareasDb.Tasks.AsNoTracking()
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
            .Take(FilasMaximas)
            .ToListAsync(ct);

        // El identificador y no el nombre: los nombres viven en Identity, y este informe ya cruza
        // dos módulos. Ponerle nombre exige un puerto nuevo, y es trabajo aparte —queda anotado
        // en la auditoría—.
        var filas = porPersona
            .Select(p => (IReadOnlyList<string>)
            [
                p.Persona.ToString(),
                p.Total.ToString(Espanol),
                p.Terminadas.ToString(Espanol),
                p.Horas.ToString("0.##", Espanol)
            ])
            .ToList();

        return new TablaDeInforme(
            informe.Name, Subtitulo(filas.Count),
            ["Persona", "Tareas", "Terminadas", "Horas estimadas"], filas);
    }

    private static string Numero(double? valor)
        => valor?.ToString("0.#", Espanol) ?? "—";

    /// <summary>
    /// La línea de contexto: cuándo se generó y cuántas filas trae.
    ///
    /// Avisa cuando el informe llegó al tope. Recortar sin decirlo es la peor opción: quien lea
    /// el fichero dará por hecho que ésos son todos los datos.
    /// </summary>
    private static string Subtitulo(int filas)
    {
        var generado = $"Generado el {DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm", Espanol)} UTC";

        return filas >= FilasMaximas
            ? $"{generado} · {filas:N0} filas (recortado: el informe tiene más datos de los que caben)"
            : $"{generado} · {filas:N0} filas";
    }
}
