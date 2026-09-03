using Microsoft.EntityFrameworkCore;
using Projects.Infrastructure.Persistence;
using Reporting.Application.Abstractions;
using Reporting.Application.DTOs;
using Ticketing.Domain.ValueObjects;
using Ticketing.Infrastructure.Persistence;
using WorkItems.Infrastructure.Persistence;

namespace ApiHost.Reporting;

/// <summary>
/// Las consultas del panel de informes, que cruzan tres módulos.
///
/// **Vive en el host y no en Reporting.Infrastructure a propósito.** Ningún módulo referencia a
/// otro; lo que necesita datos de varios se compone aquí, que es donde ya vive
/// <see cref="ApiHost.Services.PuenteDeAutomatizaciones"/> por la misma razón.
///
/// Estaba dentro del módulo, y para conseguirlo `Reporting.Infrastructure` referenciaba los
/// proyectos de Projects, WorkItems y Ticketing —incluidas sus capas de infraestructura, o sea
/// sus `DbContext`—. Seis referencias que rompían de lleno la regla de aislamiento: cualquier
/// cambio en el esquema de otro módulo llegaba hasta aquí sin pasar por ningún contrato.
///
/// Reporting sigue declarando el contrato (`IDashboardRepository`); lo que cambia es quién lo
/// implementa. El módulo dice qué necesita, el host sabe de quién sacarlo.
///
/// ---
///
/// Hubo una segunda opción, que era leer de los modelos de lectura del propio módulo. Se
/// descartó, y conviene saber por qué: los consumidores que los alimentaban sólo atendían a
/// tres eventos de **creación** —ninguno de cambio de estado, actualización o borrado—, así que
/// una tarea se quedaba en «To Do» para siempre y un proyecto al 0 % de avance. Alimentar el
/// panel con eso habría dado cifras estables y falsas, que es peor que no tenerlas. Se
/// eliminaron junto con esta refactorización.
/// </summary>
public sealed class ConsultasDelPanel(
    ProjectsDbContext projectsDb,
    WorkItemsDbContext workItemsDb,
    TicketingDbContext ticketingDb) : IDashboardRepository
{
    public async Task<KpiDataDto> GetKpiDataAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var totalProjects = await projectsDb.Projects.AsNoTracking().CountAsync(p => p.TenantId == tenantId, cancellationToken);
        var totalTasks = await workItemsDb.Tasks.AsNoTracking().CountAsync(t => t.TenantId == tenantId, cancellationToken);
        
        var doneTasks = await workItemsDb.Tasks.AsNoTracking().CountAsync(
            t => t.TenantId == tenantId && (t.Status.Value == "Done" || t.Status.Name == "Done" || t.Status.Name == "Completado"), 
            cancellationToken);

        var openTickets = await ticketingDb.Tickets.AsNoTracking().CountAsync(
            t => t.TenantId == tenantId && t.StatusValue == TicketStatus.Open.Value, 
            cancellationToken);

        var inProgressTickets = await ticketingDb.Tickets.AsNoTracking().CountAsync(
            t => t.TenantId == tenantId && (t.StatusValue == TicketStatus.InProgress.Value || t.StatusValue == TicketStatus.PendingInfo.Value), 
            cancellationToken);

        double throughput = totalTasks > 0 ? (double)doneTasks / totalTasks * 100 : 0;

        // Tiempo de entrega: de la creación al cierre, promediado sobre las tareas que tienen
        // ambas fechas. Antes esto era la constante 2,5 escrita en el código, porque la tarea
        // no guardaba ninguna marca de tiempo y no había con qué calcularlo.
        //
        // Se descartan las que no tengan las dos fechas —las cerradas antes de que existieran
        // las columnas— en vez de suponerles una: un promedio sobre fechas inventadas es peor
        // que un promedio sobre menos tareas, porque no se distingue de uno bueno.
        var duraciones = await workItemsDb.Tasks.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.CompletedAtUtc != null)
            .Select(t => EF.Functions.DateDiffSecond(t.CreatedAtUtc, t.CompletedAtUtc!.Value))
            .ToListAsync(cancellationToken);

        // Sin ninguna tarea cerrada no hay media que dar. `null` viaja hasta la interfaz, que
        // enseña un hueco; devolver 0 diría «se entrega en el acto», que es lo contrario.
        double? leadTime = duraciones.Count > 0
            ? Math.Round(duraciones.Average() / 86400.0, 1)
            : null;

        return new KpiDataDto(
            TotalProjects: totalProjects,
            TotalTasks: totalTasks,
            DoneTasks: doneTasks,
            Throughput: Math.Round(throughput, 1),
            OpenTickets: openTickets,
            InProgressTickets: inProgressTickets,
            AvgLeadTimeDays: leadTime,
            // Requiere saber cuándo la tarea entró en «En Progreso», y sólo se guarda el estado
            // actual, no su historial. Hasta que exista ese historial, este hueco es la
            // respuesta honesta. Ver el comentario de KpiDataDto.
            AvgCycleTimeDays: null
        );
    }

    public async Task<List<TaskStatusBreakdownDto>> GetTaskBreakdownAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tasks = await workItemsDb.Tasks.AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var grouped = tasks
            .GroupBy(t => t.Status.Value ?? t.Status.Name ?? "To Do")
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionary(g => g.Status, g => g.Count);

        var statusConfigs = new (string key, string label, string color)[]
        {
            ("To Do", "Por Hacer", "#94A3B8"),
            ("In Progress", "En Progreso", "#3B82F6"),
            ("In Review", "En Revisión", "#F59E0B"),
            ("Done", "Completado", "#10B981")
        };

        var result = new List<TaskStatusBreakdownDto>();

        foreach (var sc in statusConfigs)
        {
            int count = 0;
            if (grouped.TryGetValue(sc.key, out var c1)) count += c1;
            if (grouped.TryGetValue(sc.label, out var c2) && sc.key != sc.label) count += c2;

            result.Add(new TaskStatusBreakdownDto(sc.key, count, sc.color));
        }

        return result;
    }

    public async Task<List<ProjectProgressDto>> GetProjectProgressAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var projects = await projectsDb.Projects.AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var tasks = await workItemsDb.Tasks.AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var result = new List<ProjectProgressDto>();

        foreach (var p in projects)
        {
            var pTasks = tasks.Where(t => t.ProjectId == p.Id).ToList();
            int totalTasks = pTasks.Count;
            int completedTasks = pTasks.Count(t => t.Status.Value == "Done" || t.Status.Name == "Done" || t.Status.Name == "Completado");
            double progress = totalTasks > 0 ? ((double)completedTasks / totalTasks) * 100 : 0;

            result.Add(new ProjectProgressDto(
                p.Id,
                p.Name.Value,
                p.Status.Value ?? p.Status.Name ?? "Planned",
                totalTasks,
                completedTasks,
                Math.Round(progress, 1)
            ));
        }

        return result;
    }

    public async Task<ProjectBurndownDto> GetProjectBurndownAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken)
    {
        var project = await projectsDb.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == projectId, cancellationToken);

        var pTasks = await workItemsDb.Tasks.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.ProjectId == projectId)
            .ToListAsync(cancellationToken);

        string projectName = project?.Name.Value ?? "Proyecto";
        int totalTasks = pTasks.Count;

        // Lo que había aquí no era un diagrama de quemado: era una recta inventada.
        //
        //     int remaining = Math.Max(0, totalTasks - (i / 2));
        //
        // Bajaba una tarea cada dos días pasara lo que pasara, sin mirar nunca cuándo se
        // completó nada, y rellenaba el total a un mínimo de diez tareas para que la línea
        // «quedara bien» en proyectos pequeños. Un gráfico que no depende de los datos es una
        // decoración con aspecto de medida, que es peor que no tener gráfico: se toman
        // decisiones mirándolo.
        //
        // Ahora sale de `CompletedAtUtc`: para cada día, cuántas tareas seguían sin cerrar.
        var cierres = pTasks
            .Where(t => t.CompletedAtUtc.HasValue)
            .Select(t => DateOnly.FromDateTime(t.CompletedAtUtc!.Value))
            .ToList();

        // El eje va del arranque del proyecto —o de la primera tarea creada, si no hay fecha—
        // hasta hoy. No se extiende al futuro: un quemado no predice, sólo cuenta lo ocurrido.
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var inicio = project?.StartDate
            ?? (pTasks.Count > 0 ? DateOnly.FromDateTime(pTasks.Min(t => t.CreatedAtUtc)) : hoy);

        if (inicio > hoy) inicio = hoy;

        var dias = Math.Max(1, hoy.DayNumber - inicio.DayNumber);
        var dataPoints = new List<BurndownDataPointDto>();

        for (int i = 0; i <= dias; i++)
        {
            var dia = inicio.AddDays(i);

            // Lo real: las que a fecha de ese día aún no se habían cerrado.
            var restantes = totalTasks - cierres.Count(c => c <= dia);

            // Lo ideal: repartir el trabajo a ritmo constante entre el inicio y hoy. Es una
            // referencia para comparar, no una predicción.
            var ideal = (int)Math.Round(Math.Max(0, totalTasks - (i * (double)totalTasks / dias)));

            dataPoints.Add(new BurndownDataPointDto(dia.ToString("yyyy-MM-dd"), restantes, ideal));
        }

        return new ProjectBurndownDto(projectId, projectName, dataPoints);
    }
}
