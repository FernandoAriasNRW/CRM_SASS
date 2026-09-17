using System.Net.Mail;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Abstractions.Repositories;
using Ticketing.Domain.Entities;
using Ticketing.Domain.ValueObjects;

namespace Ticketing.Application.Intake;

public sealed class RevokeIntakeKeyHandler(IIntakeKeyRepository keys, ITicketingUnitOfWork unitOfWork)
    : ICommandHandler<RevokeIntakeKeyCommand, bool>
{
    public async Task<Result<bool>> Handle(RevokeIntakeKeyCommand request, CancellationToken ct)
    {
        var key = await keys.GetByIdAsync(request.TenantId, request.Id, ct);
        if (key is null)
            return Result<bool>.Failure("Clave no encontrada");

        key.Revoke(DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
