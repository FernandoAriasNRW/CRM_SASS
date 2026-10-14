namespace BuildingBlocks.Application.Abstractions;

/// <summary>
/// La zona horaria de una organización.
///
/// La usan los informes programados, para que «cada lunes a las 8» sea a las 8 de la
/// organización y no a las 8 del servidor, y las fechas de los informes exportados. La configura
/// quien administra; si no la ha cambiado, es UTC. Lo implementa Identity, y funciona también sin
/// petición, porque el planificador corre en segundo plano.
/// </summary>
public interface IOrganizationTimeZone
{
    Task<TimeZoneInfo> GetAsync(Guid tenantId, CancellationToken ct = default);
}
