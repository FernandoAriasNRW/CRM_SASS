using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Calendar.Application.DTOs;

namespace Calendar.Application.Queries;

public sealed record GetEventsQuery(
    Guid TenantId,
    DateTime? StartDate,
    DateTime? EndDate,
    string? Type,
    PaginationRequest Pagination
) : IQuery<PagedResult<CalendarEventDto>>;
