using BuildingBlocks.Domain.Primitives;

namespace Reporting.Domain.Entities;

public sealed class ProjectReadModel : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double Progress { get; set; }
    public bool IsDeleted { get; set; }
}
