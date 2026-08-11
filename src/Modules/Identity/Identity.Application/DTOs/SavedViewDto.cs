namespace Identity.Application.DTOs;

public record SavedViewDto(
    Guid Id,
    Guid UserId,
    Guid TenantId,
    string ModuleName,
    string ViewName,
    string StateJson,
    bool IsDefault
);
