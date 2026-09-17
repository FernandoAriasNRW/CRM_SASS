using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Abstractions.Repositories;

namespace Ticketing.Application;

public sealed class ChangeTicketArchiveStateHandler(
    ITicketRepository repository,
    ITicketingUnitOfWork unitOfWork) : ICommandHandler<ChangeTicketArchiveStateCommand, bool>
{
    public async Task<Result<bool>> Handle(ChangeTicketArchiveStateCommand request, CancellationToken ct)
    {
        // Incluyendo lo oculto a propósito: restaurar exige encontrar lo que el filtro esconde.
        var ticket = await repository.GetIncludingHiddenAsync(request.TenantId, request.Id, ct);
        if (ticket is null)
            return Result<bool>.Failure("Ticket no encontrado");

        switch (request.Action)
        {
            case ArchiveAction.Archive: ticket.Archive(); break;
            case ArchiveAction.Unarchive: ticket.Unarchive(); break;
            case ArchiveAction.MoveToTrash: ticket.MoveToTrash(); break;
            case ArchiveAction.RestoreFromTrash: ticket.RestoreFromTrash(); break;
        }

        await repository.UpdateAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
