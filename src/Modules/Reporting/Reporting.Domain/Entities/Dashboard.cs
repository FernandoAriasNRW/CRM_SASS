using BuildingBlocks.Domain.Primitives;

namespace Reporting.Domain.Entities;

public sealed class Dashboard : Entity
{
    public Guid TenantId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }
    public bool IsPublic { get; private set; }
    public Guid CreatedById { get; private set; }
    public string WidgetsJson { get; private set; } = string.Empty;
    public List<Guid> TagIds { get; private set; } = new();

    private Dashboard() { }

    public static Dashboard Create(Guid tenantId, string title, bool isDefault, bool isPublic, Guid createdById, string widgetsJson)
    {
        return new Dashboard
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title,
            IsDefault = isDefault,
            IsPublic = isPublic,
            CreatedById = createdById,
            WidgetsJson = widgetsJson
        };
    }

    public void Update(string title, bool isDefault, bool isPublic, string widgetsJson)
    {
        Title = title;
        IsDefault = isDefault;
        IsPublic = isPublic;
        WidgetsJson = widgetsJson;
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
