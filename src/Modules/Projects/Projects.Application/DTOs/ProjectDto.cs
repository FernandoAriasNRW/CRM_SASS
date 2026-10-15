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
    Guid OwnerId,
    /// <summary>Las etiquetas (ids del módulo Tags); la pantalla las resuelve con <c>GET /tags</c>.</summary>
    IReadOnlyList<Guid>? TagIds = null,
    /// <summary>Cuándo se dio de alta. Es lo que desempata a los que empiezan el mismo día.</summary>
    DateTime? CreatedAtUtc = null
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
