using BuildingBlocks.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Infrastructure.Storage;

/// <summary>
/// Guarda los ficheros en el disco del servidor.
///
/// <b>Existe porque el otro no funciona.</b> El único <see cref="IStorageService"/> que había subía
/// a Cloudinary, y <b>Cloudinary no está configurado en ninguna parte</b>: ni en los ajustes de
/// desarrollo, ni en los de producción, ni en el compose. Subir un fichero devolvía
/// «Cloud name must be specified in Account!» con un 400. No es que le faltara quien lo llamara
/// desde la pantalla: es que nunca ha llegado a subir nada.
///
/// Con esto, adjuntar un fichero funciona nada más levantar el proyecto. Cuando haya credenciales
/// de Cloudinary, se usa Cloudinary sin tocar nada: la elección se hace al registrar el servicio.
///
/// <b>No sustituye a un almacenamiento de verdad para producción</b> —un disco local no se comparte
/// entre réplicas ni sobrevive a recrear el contenedor si no está en un volumen— pero es honesto:
/// hace lo que dice, y lo que había no hacía nada.
/// </summary>
public sealed class DiskStorageService : IStorageService
{
    private readonly DiskStorageOptions _options;
    private readonly ILogger<DiskStorageService> _logger;

    public DiskStorageService(IOptions<DiskStorageOptions> options, ILogger<DiskStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Dónde acaban los ficheros, resolviendo la configuración.
    ///
    /// Es estático y público porque lo necesitan dos sitios: este servicio, para escribir, y el
    /// arranque, para servir esa misma carpeta. Calcularlo dos veces es la forma de que un día
    /// dejen de coincidir y los ficheros se suban a un sitio del que no se pueden leer.
    /// </summary>
    public static string ResolveFolder(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
            return Path.Combine(Path.GetTempPath(), "crm-saas-storage");

        return Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(Directory.GetCurrentDirectory(), configured);
    }

    public async Task<string> UploadFileAsync(
        Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        var folder = ResolveFolder(_options.Folder);

        Directory.CreateDirectory(folder);

        var name = SafeFileName(fileName);
        var destination = Path.Combine(folder, name);

        await using (var output = File.Create(destination))
        {
            await fileStream.CopyToAsync(output, ct);
        }

        _logger.LogInformation("Fichero guardado en disco: {Nombre}", name);

        return $"{_options.PublicPath.TrimEnd('/')}/{Uri.EscapeDataString(name)}";
    }

    public Task DeleteFileAsync(string fileUrl, CancellationToken ct = default)
    {
        var name = Path.GetFileName(new Uri(fileUrl, UriKind.RelativeOrAbsolute).ToString());
        var folder = ResolveFolder(_options.Folder);

        var destination = Path.Combine(folder, Uri.UnescapeDataString(name));

        // Se comprueba que el fichero está dentro de la carpeta antes de borrarlo. Sin esto, una
        // dirección con «..» dentro borraría cualquier cosa a la que llegue el proceso.
        if (destination.StartsWith(folder, StringComparison.Ordinal) && File.Exists(destination))
            File.Delete(destination);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Un nombre que no puede escaparse de la carpeta ni pisar a otro.
    ///
    /// Se descarta la ruta que traiga el cliente y se conserva sólo el nombre; delante va un
    /// identificador, así que dos personas subiendo «captura.png» no se pisan.
    /// </summary>
    private static string SafeFileName(string fileName)
    {
        var baseName = Path.GetFileName(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "fichero";

        foreach (var invalidChar in Path.GetInvalidFileNameChars())
            baseName = baseName.Replace(invalidChar, '-');

        if (baseName.Length > 100) baseName = baseName[^100..];

        return $"{Guid.NewGuid():N}-{baseName}";
    }
}
