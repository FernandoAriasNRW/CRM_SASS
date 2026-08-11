using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Calendar.Application.Abstractions.Queries;
using Calendar.Application.Queries;
using Calendar.Application.DTOs;

namespace Calendar.Application.Handlers.Queries;

/// <summary>
/// Handler para obtener un evento por ID.
/// Usa ICalendarEventQueries (separación CQRS).
/// </summary>
public sealed class GetEventByIdHandler(
    ICalendarEventQueries queries) : IQueryHandler<GetEventByIdQuery, CalendarEventDto?>
{
    private readonly ICalendarEventQueries _queries = queries;

    public async Task<Result<CalendarEventDto?>> Handle(GetEventByIdQuery request, CancellationToken ct)
    {
        var dto = await _queries.GetByIdAsync(request.TenantId, request.EventId, ct);

        if (dto is null)
            return Result<CalendarEventDto?>.Failure("Evento de calendario no encontrado");

        return Result<CalendarEventDto?>.Success(dto);
    }
}

/// <summary>
/// Handler para obtener eventos con paginación.
/// Usa ICalendarEventQueries (separación CQRS).
/// </summary>
public sealed class GetEventsHandler(
    ICalendarEventQueries queries) : IQueryHandler<GetEventsQuery, PagedResult<CalendarEventDto>>
{
    private readonly ICalendarEventQueries _queries = queries;

    public async Task<Result<PagedResult<CalendarEventDto>>> Handle(GetEventsQuery request, CancellationToken ct)
    {
        var result = await _queries.GetByTenantAsync(
            request.TenantId,
            request.StartDate,
            request.EndDate,
            request.Type,
            request.Pagination,
            ct);

        return Result<PagedResult<CalendarEventDto>>.Success(result);
    }
}