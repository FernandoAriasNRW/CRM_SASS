using System.Net.Mail;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Abstractions.Repositories;
using Ticketing.Domain.Entities;
using Ticketing.Domain.ValueObjects;

namespace Ticketing.Application.Intake;

public sealed class GetIntakeKeysHandler(IIntakeKeyRepository keys)
    : IQueryHandler<GetIntakeKeysQuery, List<IntakeKeyDto>>
{
    public async Task<Result<List<IntakeKeyDto>>> Handle(GetIntakeKeysQuery request, CancellationToken ct)
    {
        var list = await keys.GetByTenantAsync(request.TenantId, ct);
        return Result<List<IntakeKeyDto>>.Success(list
            .Select(c => new IntakeKeyDto(c.Id, c.Name, c.Prefix, c.CreatedAtUtc, c.LastUsedAtUtc, c.RevokedAtUtc))
            .ToList());
    }
}
