using BuildingBlocks.Domain.Primitives;

namespace Reporting.Domain.Entities;

public sealed class TicketReadModel : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public int Status { get; set; }
}
