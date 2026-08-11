using BuildingBlocks.Application.Abstractions;
using Calendar.Application.DTOs;

namespace Calendar.Application.Queries;

public sealed record GetEventByIdQuery(Guid TenantId, Guid EventId) : IQuery<CalendarEventDto?>;
