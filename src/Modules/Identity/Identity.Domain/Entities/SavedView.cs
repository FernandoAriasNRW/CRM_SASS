using BuildingBlocks.Domain.Primitives;

namespace Identity.Domain.Entities;

public sealed class SavedView : Entity
{
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public string ModuleName { get; private set; } = string.Empty;
    public string ViewName { get; private set; } = string.Empty;
    public string StateJson { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }

    private SavedView() { }

    public static SavedView Create(Guid userId, Guid tenantId, string moduleName, string viewName, string stateJson, bool isDefault)
    {
        return new SavedView
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            ModuleName = moduleName,
            ViewName = viewName,
            StateJson = stateJson,
            IsDefault = isDefault
        };
    }

    public void UpdateState(string stateJson)
    {
        StateJson = stateJson;
    }

    public void Rename(string viewName)
    {
        ViewName = viewName;
    }

    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
    }
}
