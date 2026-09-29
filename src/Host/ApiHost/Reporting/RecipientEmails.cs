using BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Exports;
using Reporting.Application.Schedules;
using Reporting.Domain.Entities;
using Reporting.Infrastructure.Persistence;

namespace ApiHost.Reporting;

/// <summary>
/// La dirección de correo de una persona.
///
/// <b>Vive en el host</b> porque cruza módulos: Reporting sabe a quién quiere avisar y sólo
/// Identity sabe su correo, y ninguno referencia al otro. Es el mismo reparto de siempre.
/// </summary>
public sealed class RecipientEmails(Identity.Infrastructure.Persistence.IdentityDbContext identityDb)
{
    public async Task<string?> EmailOfAsync(Guid tenantId, Guid userId, CancellationToken ct)
        => await identityDb.User
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.Id == userId)
            .Select(u => u.Email.Value)
            .FirstOrDefaultAsync(ct);
}
