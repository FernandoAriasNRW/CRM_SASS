using System.Net.Mail;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Abstractions.Repositories;
using Ticketing.Domain.Entities;
using Ticketing.Domain.ValueObjects;

namespace Ticketing.Application.Intake;

public sealed class CreateIntakeKeyHandler(IIntakeKeyRepository keys, ITicketingUnitOfWork unitOfWork)
    : ICommandHandler<CreateIntakeKeyCommand, CreatedIntakeKeyDto>
{
    public async Task<Result<CreatedIntakeKeyDto>> Handle(CreateIntakeKeyCommand request, CancellationToken ct)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 100)
            return Result<CreatedIntakeKeyDto>.Failure("El nombre de la clave es obligatorio y admite hasta 100 caracteres");

        var (key, plainText) = IntakeKey.Generate(request.TenantId, name, request.CreatedBy, DateTime.UtcNow);
        await keys.AddAsync(key, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<CreatedIntakeKeyDto>.Success(new(key.Id, key.Name, key.Prefix, plainText));
    }
}
