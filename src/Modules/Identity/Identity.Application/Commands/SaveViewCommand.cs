using BuildingBlocks.Application.Abstractions;
using Identity.Application.DTOs;

namespace Identity.Application.Commands;

public record SaveViewCommand(
    Guid TenantId,
    Guid UserId,
    string ModuleName,
    string ViewName,
    string StateJson,
    bool IsDefault
) : ICommand<SavedViewDto>;

public record DeleteSavedViewCommand(
    Guid TenantId,
    Guid UserId,
    Guid ViewId
) : ICommand<bool>;
