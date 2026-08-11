using Microsoft.EntityFrameworkCore;
using Projects.Infrastructure.Persistence;
using Reporting.Application.Abstractions;
using Reporting.Application.DTOs;
using Ticketing.Domain.ValueObjects;
using Ticketing.Infrastructure.Persistence;
using WorkItems.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Repositories;

public sealed class DashboardRepository(
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

        return new KpiDataDto(
            TotalProjects: totalProjects,
            TotalTasks: totalTasks,
            DoneTasks: doneTasks,
            Throughput: Math.Round(throughput, 1),
            OpenTickets: openTickets,
            InProgressTickets: inProgressTickets,
            AvgLeadTimeDays: 2.5,
            AvgCycleTimeDays: 1.4
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
        int totalTasks = Math.Max(pTasks.Count, 10);
        var dataPoints = new List<BurndownDataPointDto>();

        var startDate = project?.StartDate.ToDateTime(TimeOnly.MinValue) ?? DateTime.UtcNow.AddDays(-10);
        for (int i = 0; i <= 14; i++)
        {
            var currentDate = startDate.AddDays(i);
            double idealRemaining = Math.Max(0, totalTasks - (i * ((double)totalTasks / 14)));
            int remaining = Math.Max(0, totalTasks - (i / 2));

            dataPoints.Add(new BurndownDataPointDto(
                currentDate.ToString("yyyy-MM-dd"),
                remaining,
                (int)Math.Round(idealRemaining)
            ));
        }

        return new ProjectBurndownDto(projectId, projectName, dataPoints);
    }
}
