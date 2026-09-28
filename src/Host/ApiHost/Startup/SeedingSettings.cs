namespace ApiHost.Startup;

/// <summary>
/// Qué se siembra y cuándo. <b>Todo apagado por defecto, en cualquier entorno.</b>
///
/// Hasta septiembre de 2026 la siembra de demostración corría sola en cada arranque —también en
/// producción— y, si la base no tenía usuarios, se creaba <c>admin@acme.com</c> con la contraseña
/// <c>admin123</c>. Además, los sembradores se llevaban a la organización de demostración los datos
/// de todas las demás (ver <c>OrphanRows</c> y <c>docs/AUDITORIA.md</c> §21). Ahora cada cosa se
/// enciende a mano, en la configuración del entorno que la necesite:
///
/// <code>
/// DemoData__SeedOnStartup=true       sembrar la demostración al arrancar
/// DemoData__AllowSeedEndpoint=true   exponer POST /api/v1/admin/seed-database (sólo Admin)
/// InitialAdmin__Email=...            primer administrador, si la base no tiene usuarios
/// InitialAdmin__Password=...
/// </code>
/// </summary>
public sealed record SeedingSettings(
    bool SeedOnStartup,
    bool AllowSeedEndpoint,
    string? InitialAdminEmail,
    string? InitialAdminPassword)
{
    /// <summary>
    /// Por debajo de esto una contraseña de arranque se adivina. Se comprueba al arrancar y no al
    /// iniciar sesión porque es la única vez que el sistema la ve en claro.
    /// </summary>
    public const int MinInitialAdminPasswordLength = 12;

    public static SeedingSettings From(IConfiguration configuration) => new(
        configuration.GetValue("DemoData:SeedOnStartup", false),
        configuration.GetValue("DemoData:AllowSeedEndpoint", false),
        NullIfBlank(configuration["InitialAdmin:Email"]),
        NullIfBlank(configuration["InitialAdmin:Password"]));

    public bool HasInitialAdmin => InitialAdminEmail is not null && InitialAdminPassword is not null;

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
