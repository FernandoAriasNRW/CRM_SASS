using BuildingBlocks.Application.Abstractions;
using Identity.Application.Organization;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

public sealed class OrganizationSettingsRepository(IdentityDbContext context) : IOrganizationSettingsRepository
{
    public Task<OrganizationSettings?> FindAsync(Guid tenantId, CancellationToken ct)
        => context.OrganizationSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

    public async Task AddAsync(OrganizationSettings settings, CancellationToken ct)
        => await context.OrganizationSettings.AddAsync(settings, ct);

    public Task SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
}

/// <summary>
/// Responde <see cref="IOrganizationTimeZone"/>. Con el inquilino forzado, como
/// <c>UserDirectory</c>: lo pregunta el planificador de informes, que no tiene petición.
/// </summary>
internal sealed class OrganizationTimeZone(IdentityDbContext context) : IOrganizationTimeZone
{
    public async Task<TimeZoneInfo> GetAsync(Guid tenantId, CancellationToken ct = default)
    {
        using var _ = context.AsTenant(tenantId);

        var name = await context.OrganizationSettings.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.TimeZone)
            .FirstOrDefaultAsync(ct);

        // Una zona guardada que este sistema ya no conoce —un tzdata viejo, una zona retirada—
        // cae en UTC en vez de tumbar el informe.
        return OrganizationSettings.TryFindTimeZone(name, out var zone) ? zone : TimeZoneInfo.Utc;
    }
}
