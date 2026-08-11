using BuildingBlocks.Application.Abstractions;

namespace Calendar.Application.Commands;


public sealed record PermanentDeleteEventCommand(
    Guid TenantId,
    Guid EventId,
    Guid DeletedBy
) : ICommand<bool>;
