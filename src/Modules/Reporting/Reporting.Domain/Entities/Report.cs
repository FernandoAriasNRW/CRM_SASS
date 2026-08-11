using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;
using Reporting.Domain.Events;
using Reporting.Domain.ValueObjects;

namespace Reporting.Domain.Entities;

public sealed class Report : AggregateRoot, ITenantEntity, ISoftDeletable
{
    public Guid TenantId { get; private set; }
    public Guid CreatedById { get; private set; }
    public List<Guid> TagIds { get; private set; } = new();
    public string Name { get; private set; } = string.Empty;
    public int TypeValue { get; private set; }
    public int FormatValue { get; private set; }
    public string? Parameters { get; private set; }
    public string? GeneratedFileUrl { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? GeneratedAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool IsGenerated { get; private set; }
    public bool IsDeleted { get; private set; }

    public ReportType Type => ReportType.FromValue<ReportType>(TypeValue);
    public ReportFormat Format => ReportFormat.FromValue<ReportFormat>(FormatValue);

    private Report() { }

    public static Result<Report> Create(
        Guid tenantId,
        Guid createdById,
        string name,
        ReportType type,
        ReportFormat format,
        string? parameters = null)
    {
        var nameResult = ReportName.Create(name);
        if (nameResult.IsFailure)
            return Result<Report>.Failure(nameResult.Error!);

        var report = new Report
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CreatedById = createdById,
            Name = name,
            TypeValue = type.Value,
            FormatValue = format.Value,
            Parameters = parameters,
            CreatedAt = DateTime.UtcNow
        };

        report.RaiseDomainEvent(new ReportCreatedEvent(report.Id, tenantId, createdById));
        return Result<Report>.Success(report);
    }

    public void MarkAsGenerated(string fileUrl)
    {
        GeneratedFileUrl = fileUrl;
        GeneratedAt = DateTime.UtcNow;
        IsGenerated = true;
        RaiseDomainEvent(new ReportGeneratedEvent(Id, TenantId, fileUrl));
    }

    public void MarkAsFailed(string error)
    {
        ErrorMessage = error;
        RaiseDomainEvent(new ReportGenerationFailedEvent(Id, TenantId, error));
    }

    public void AddTag(Guid tagId)
    {
        if (!TagIds.Contains(tagId))
        {
            TagIds.Add(tagId);
        }
    }

    public void RemoveTag(Guid tagId)
    {
        if (TagIds.Contains(tagId))
        {
            TagIds.Remove(tagId);
        }
    }
}
