namespace ApiHost.Startup;

/// <summary>
/// Validación de configuración — fail fast.
///
/// Preferimos que la aplicación no arranque a que arranque con una configuración insegura. Sin
/// esto, una clave JWT vacía produce tokens que cualquiera puede falsificar, y el fallo aparecería
/// mucho más tarde y de forma confusa.
/// </summary>
public static class RequiredSettings
{
    /// <summary>Comprueba lo imprescindible y devuelve la clave JWT ya validada.</summary>
    public static string EnsureValid(IConfiguration configuration)
    {
        var jwtKey = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            throw new InvalidOperationException(
                "Falta la configuración 'Jwt:Key'. Defínala en appsettings.Development.json, " +
                "en user-secrets (dotnet user-secrets set \"Jwt:Key\" \"<valor>\") " +
                "o en la variable de entorno Jwt__Key.");
        }

        // HMAC-SHA256 requiere una clave de al menos 256 bits (32 bytes).
        if (System.Text.Encoding.UTF8.GetByteCount(jwtKey) < 32)
        {
            throw new InvalidOperationException(
                "'Jwt:Key' debe tener al menos 32 caracteres para firmar con HMAC-SHA256. " +
                "Genere una con: openssl rand -base64 48");
        }

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Falta la cadena de conexión 'ConnectionStrings:DefaultConnection'. " +
                "Defínala en appsettings.Development.json o en la variable de entorno " +
                "ConnectionStrings__DefaultConnection.");
        }

        return jwtKey;
    }
}
