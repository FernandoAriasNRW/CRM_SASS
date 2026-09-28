using BuildingBlocks.Application.Abstractions;
using Calendar.Application.DTOs;

namespace Calendar.Application.Commands;

/// <summary>Deshace una anulación: la reunión vuelve a estar en pie.</summary>
public sealed record ReactivateEventCommand(
    Guid TenantId,
    Guid EventId,
    Guid UserId
) : ICommand<CalendarEventDto>;
