using BuildingBlocks.Application.Abstractions;

namespace Tags.Application.Commands;

/// <summary>Borra una etiqueta. La autorización, como en la edición, la hace el handler.</summary>
public sealed record DeleteTagCommand(Guid TenantId, Guid UserId, Guid TagId) : ICommand;
