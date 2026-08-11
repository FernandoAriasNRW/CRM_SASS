using MassTransit;
using Microsoft.Extensions.Logging;
using Projects.Domain.Events;
using Reporting.Domain.Entities;
using Reporting.Infrastructure.Persistence;
using Ticketing.Domain.Events;
using WorkItems.Domain.Events;

namespace Reporting.Infrastructure.Consumers;

public sealed class ProjectCreatedConsumer(ReportingDbContext dbContext, ILogger<ProjectCreatedConsumer> logger) : IConsumer<ProjectCreatedEvent>
{
    public async Task Consume(ConsumeContext<ProjectCreatedEvent> context)
    {
        logger.LogInformation("Reporting module received ProjectCreatedEvent for {ProjectId}", context.Message.ProjectId);
        
        var rm = new ProjectReadModel
        {
            Id = context.Message.ProjectId,
            TenantId = context.Message.TenantId,
            Name = context.Message.Name,
            Status = "Active",
            Progress = 0,
            IsDeleted = false
        };
        dbContext.Projects.Add(rm);
        await dbContext.SaveChangesAsync();
    }
}

public sealed class TaskCreatedConsumer(ReportingDbContext dbContext, ILogger<TaskCreatedConsumer> logger) : IConsumer<TaskCreatedEvent>
{
    public async Task Consume(ConsumeContext<TaskCreatedEvent> context)
    {
        logger.LogInformation("Reporting module received TaskCreatedEvent for {TaskId}", context.Message.TaskId);
        
        var rm = new TaskReadModel
        {
            Id = context.Message.TaskId,
            TenantId = context.Message.TenantId,
            ProjectId = context.Message.ProjectId,
            AssigneeId = context.Message.AssigneeId,
            Status = "To Do"
        };
        dbContext.Tasks.Add(rm);
        await dbContext.SaveChangesAsync();
    }
}

public sealed class TicketCreatedConsumer(ReportingDbContext dbContext, ILogger<TicketCreatedConsumer> logger) : IConsumer<TicketCreatedEvent>
{
    public async Task Consume(ConsumeContext<TicketCreatedEvent> context)
    {
        logger.LogInformation("Reporting module received TicketCreatedEvent for {TicketId}", context.Message.TicketId);
        
        var rm = new TicketReadModel
        {
            Id = context.Message.TicketId,
            TenantId = context.Message.TenantId,
            Status = 0 // Assuming 0 is Open/New
        };
        dbContext.Tickets.Add(rm);
        await dbContext.SaveChangesAsync();
    }
}
