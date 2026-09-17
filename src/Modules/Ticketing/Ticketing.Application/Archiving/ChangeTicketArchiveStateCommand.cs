using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Abstractions.Repositories;

namespace Ticketing.Application;

/// <summary>
/// Archivar, desarchivar, tirar a la papelera y restaurar un ticket.
///
/// Un solo comando con una acción, y no cuatro: las cuatro hacen lo mismo —cargar, cambiar un
/// campo, guardar— y separarlas serían cuatro copias del mismo handler esperando a divergir.
/// </summary>
public sealed record ChangeTicketArchiveStateCommand(
    Guid TenantId,
    Guid Id,
    ArchiveAction Action) : ICommand<bool>, IAuthorizeEntity
{
    public string EntityType => "Ticket";
    public Guid EntityId => Id;
    public string RequiredPermission => "Write";
}
