using Microsoft.EntityFrameworkCore;
using Projects.Infrastructure.Persistence;
using Ticketing.Infrastructure.Persistence;
using WorkItems.Infrastructure.Persistence;

namespace ApiHost.Notifications;

/// <summary>
/// Quién tiene que ver con cada cosa: a quién le interesa lo que pasa en una tarea, un ticket o un
/// proyecto.
///
/// Es lo que se pidió: <b>quien la creó y quien la tiene asignada</b>. En una tarea, su autor y sus
/// responsables —el principal y los demás—; en un ticket, quien lo abrió y el agente que lo lleva;
/// en un proyecto, su dueño.
///
/// Vive en el host porque lee de tres módulos, como <see cref="ApiHost.Services.AutomationNotifier"/>.
/// Sin filtros globales y con el inquilino por escrito: una tarea recién borrada tiene que poder
/// decir a quién avisar de que se borró.
/// </summary>
public sealed class InterestedParties(
    WorkItemsDbContext tasks,
    TicketingDbContext tickets,
    ProjectsDbContext projects)
{
    public sealed record People(string Title, IReadOnlyList<Guid> Creators, IReadOnlyList<Guid> Assignees)
    {
        public IEnumerable<Guid> Everyone => Creators.Concat(Assignees).Distinct();
    }

    public async Task<People?> TaskAsync(Guid tenantId, Guid taskId, CancellationToken ct)
    {
        var task = await tasks.Tasks.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.Id == taskId)
            .Select(t => new { Title = t.Title.Value, t.CreatedById, t.AssigneeId, Others = t.Assignees.Select(a => a.UserId).ToList() })
            .FirstOrDefaultAsync(ct);

        return task is null ? null : new People(task.Title, [task.CreatedById],
            new[] { task.AssigneeId }.Concat(task.Others).Where(id => id != Guid.Empty).Distinct().ToList());
    }

    public async Task<People?> TicketAsync(Guid tenantId, Guid ticketId, CancellationToken ct)
    {
        var ticket = await tickets.Tickets.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.Id == ticketId)
            .Select(t => new { t.Title, t.CustomerId, t.AssignedAgentId })
            .FirstOrDefaultAsync(ct);

        return ticket is null ? null : new People(ticket.Title, [ticket.CustomerId],
            ticket.AssignedAgentId is { } agent && agent != Guid.Empty ? [agent] : []);
    }

    public async Task<People?> ProjectAsync(Guid tenantId, Guid projectId, CancellationToken ct)
    {
        var project = await projects.Projects.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.Id == projectId)
            .Select(p => new { Name = p.Name.Value, p.OwnerId })
            .FirstOrDefaultAsync(ct);

        return project is null ? null : new People(project.Name, [project.OwnerId], []);
    }

    /// <summary>Los interesados de lo que se comenta, según su tipo.</summary>
    public Task<People?> OfAsync(Guid tenantId, string entityType, Guid entityId, CancellationToken ct) => entityType switch
    {
        BuildingBlocks.Domain.EntityTypes.Task => TaskAsync(tenantId, entityId, ct),
        BuildingBlocks.Domain.EntityTypes.Ticket => TicketAsync(tenantId, entityId, ct),
        BuildingBlocks.Domain.EntityTypes.Project => ProjectAsync(tenantId, entityId, ct),
        _ => Task.FromResult<People?>(null),
    };
}
