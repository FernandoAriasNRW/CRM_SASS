using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;

namespace Identity.Domain.Entities;

/// <summary>
/// Lo que se configura para toda una organización. Por ahora, su zona horaria.
///
/// <b>Una fila por organización, y puede no existir.</b> No hay entidad «organización»: el
/// inquilino es un identificador. Mientras nadie cambie nada, no se guarda fila y vale lo de
/// <see cref="DefaultTimeZone"/>.
///
/// <b>La zona horaria es la de la organización, no la de cada persona.</b> La usan los informes
/// programados —«cada lunes a las 8» es a las 8 de la organización— y las fechas de los informes
/// exportados. El calendario sigue con la hora del navegador de cada uno.
/// </summary>
public sealed class OrganizationSettings : Entity, ITenantEntity
{
    /// <summary>
    /// La zona de partida. Es la que se usaba de hecho —la del servidor, que corre en UTC— hasta
    /// que existió esta configuración, así que no cambia nada para quien no la toque.
    /// </summary>
    public const string DefaultTimeZone = "UTC";

    public const int MaxTimeZoneLength = 64;

    public Guid TenantId { get; private set; }

    /// <summary>El nombre IANA de la zona: <c>America/Bogota</c>, <c>Europe/Madrid</c>, <c>UTC</c>.</summary>
    public string TimeZone { get; private set; } = DefaultTimeZone;

    public DateTime UpdatedAtUtc { get; private set; }

    private OrganizationSettings() { }

    public static OrganizationSettings Create(DateTime nowUtc, Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        TimeZone = DefaultTimeZone,
        UpdatedAtUtc = nowUtc,
    };

    public Result ChangeTimeZone(DateTime nowUtc, string? timeZone)
    {
        var name = timeZone?.Trim() ?? string.Empty;
        if (!TryFindTimeZone(name, out _))
            return Result.Failure(Rules.UnknownTimeZone);

        TimeZone = name;
        UpdatedAtUtc = nowUtc;
        return Result.Success();
    }

    /// <summary>
    /// Busca una zona por su nombre IANA.
    ///
    /// <b>Solo nombres IANA</b> (con «/», o <c>UTC</c>). .NET en Windows acepta también los nombres
    /// de Windows —«Romance Standard Time»—, pero la API corre en Linux, donde esos no existen: una
    /// zona guardada desde una máquina Windows dejaría de encontrarse en producción.
    /// </summary>
    public static bool TryFindTimeZone(string? name, out TimeZoneInfo zone)
    {
        zone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxTimeZoneLength) return false;
        if (name != DefaultTimeZone && !name.Contains('/')) return false;

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(name, out var found)) return false;
        zone = found;
        return true;
    }

    public static class Rules
    {
        public const string UnknownTimeZone =
            "Esa zona horaria no existe. Usa un nombre como «America/Bogota» o «Europe/Madrid»";
    }
}
