namespace Reporting.Domain.Entities;

public sealed class TaskReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid AssigneeId { get; set; }
    public string Status { get; set; } = string.Empty;
}
