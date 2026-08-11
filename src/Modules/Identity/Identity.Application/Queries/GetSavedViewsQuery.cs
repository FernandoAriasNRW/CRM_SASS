using BuildingBlocks.Application.Abstractions;
using Identity.Application.DTOs;

namespace Identity.Application.Queries;

public record GetSavedViewsQuery(
    Guid TenantId,
    Guid UserId,
    string ModuleName
) : IQuery<IReadOnlyList<SavedViewDto>>;
