using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Domain.Entities;

namespace Identity.Application.Organization;

/// <summary>La configuración de la organización tal como sale de la API.</summary>
public sealed record OrganizationSettingsDto(string TimeZone);

public interface IOrganizationSettingsRepository
{
    /// <summary>La fila de la organización, o <c>null</c> si nunca se cambió nada.</summary>
    Task<OrganizationSettings?> FindAsync(Guid tenantId, CancellationToken ct);

    Task AddAsync(OrganizationSettings settings, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}

public sealed record GetOrganizationSettingsQuery(Guid TenantId) : IQuery<OrganizationSettingsDto>;

public sealed class GetOrganizationSettingsHandler(IOrganizationSettingsRepository repository)
    : IQueryHandler<GetOrganizationSettingsQuery, OrganizationSettingsDto>
{
    public async Task<Result<OrganizationSettingsDto>> Handle(GetOrganizationSettingsQuery request, CancellationToken ct)
    {
        var settings = await repository.FindAsync(request.TenantId, ct);
        return Result<OrganizationSettingsDto>.Success(
            new OrganizationSettingsDto(settings?.TimeZone ?? OrganizationSettings.DefaultTimeZone));
    }
}

/// <summary>Cambia la zona horaria de la organización. Solo quien administra (lo exige el endpoint).</summary>
public sealed record ChangeOrganizationTimeZoneCommand(Guid TenantId, string TimeZone) : ICommand<OrganizationSettingsDto>;

public sealed class ChangeOrganizationTimeZoneHandler(TimeProvider timeProvider, IOrganizationSettingsRepository repository)
    : ICommandHandler<ChangeOrganizationTimeZoneCommand, OrganizationSettingsDto>
{
    public async Task<Result<OrganizationSettingsDto>> Handle(ChangeOrganizationTimeZoneCommand request, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var settings = await repository.FindAsync(request.TenantId, ct);
        var isNew = settings is null;
        settings ??= OrganizationSettings.Create(now, request.TenantId);

        var changed = settings.ChangeTimeZone(now, request.TimeZone);
        if (changed.IsFailure)
            return Result<OrganizationSettingsDto>.Failure(changed.Error!);

        if (isNew) await repository.AddAsync(settings, ct);
        await repository.SaveChangesAsync(ct);
        return Result<OrganizationSettingsDto>.Success(new OrganizationSettingsDto(settings.TimeZone));
    }
}
