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
public sealed record CambiarArchivoDeTicketCommand(
    Guid TenantId,
    Guid Id,
    ArchiveAction Accion) : ICommand<bool>, IAuthorizeEntity
{
    public string EntityType => "Ticket";
    public Guid EntityId => Id;
    public string RequiredPermission => "Write";
}

public sealed class CambiarArchivoDeTicketHandler(
    ITicketRepository repository,
    ITicketingUnitOfWork unitOfWork) : ICommandHandler<CambiarArchivoDeTicketCommand, bool>
{
    public async Task<Result<bool>> Handle(CambiarArchivoDeTicketCommand request, CancellationToken ct)
    {
        // Incluyendo lo oculto a propósito: restaurar exige encontrar lo que el filtro esconde.
        var ticket = await repository.GetIncluyendoOcultosAsync(request.TenantId, request.Id, ct);
        if (ticket is null)
            return Result<bool>.Failure("Ticket no encontrado");

        switch (request.Accion)
        {
            case ArchiveAction.Archive: ticket.Archivar(); break;
            case ArchiveAction.Unarchive: ticket.Desarchivar(); break;
            case ArchiveAction.MoveToTrash: ticket.MoveToTrash(); break;
            case ArchiveAction.RestoreFromTrash: ticket.RestoreFromTrash(); break;
        }

        await repository.UpdateAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
