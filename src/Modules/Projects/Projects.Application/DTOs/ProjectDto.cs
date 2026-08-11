namespace Projects.Application.DTOs;

public sealed record ProjectDto(
    Guid Id,
    Guid TenantId,
    Guid SpaceId,
    Guid? FolderId,
    string Name,
    string Description,
    DateOnly StartDate,
    DateOnly EstimatedEndDate,
    string Status,
    Guid OwnerId
);

public sealed record ProjectsPaginatedResponse(
    IReadOnlyList<ProjectDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage
);
