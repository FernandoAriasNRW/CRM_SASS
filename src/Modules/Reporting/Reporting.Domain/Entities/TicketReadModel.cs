namespace Reporting.Domain.Entities;

public sealed class TicketReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public int Status { get; set; }
}
