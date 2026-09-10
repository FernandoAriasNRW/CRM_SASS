using BuildingBlocks.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Infrastructure.Storage;

/// <summary>Dónde se guardan los ficheros y con qué dirección se sirven.</summary>
public sealed class OpcionesDeDisco
{
    /// <summary>
    /// Carpeta del servidor. Vacía significa la de por defecto, dentro del temporal del sistema.
    ///
    /// El valor por defecto **no** cuelga del directorio de la aplicación: la imagen de Docker
    /// corre con un usuario sin privilegios y `/app` no es escribible, así que crear la carpeta
    /// ahí tiraba el arranque entero.
    /// </summary>
    public string Carpeta { get; set; } = string.Empty;

    /// <summary>Prefijo con el que se publican. Tiene que coincidir con el de los ficheros estáticos.</summary>
    public string RutaPublica { get; set; } = "/almacen";
}

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
public sealed class AlmacenamientoEnDisco : IStorageService
{
    private readonly OpcionesDeDisco _opciones;
    private readonly ILogger<AlmacenamientoEnDisco> _logger;

    public AlmacenamientoEnDisco(IOptions<OpcionesDeDisco> opciones, ILogger<AlmacenamientoEnDisco> logger)
    {
        _opciones = opciones.Value;
        _logger = logger;
    }

    /// <summary>
    /// Dónde acaban los ficheros, resolviendo la configuración.
    ///
    /// Es estático y público porque lo necesitan dos sitios: este servicio, para escribir, y el
    /// arranque, para servir esa misma carpeta. Calcularlo dos veces es la forma de que un día
    /// dejen de coincidir y los ficheros se suban a un sitio del que no se pueden leer.
    /// </summary>
    public static string CarpetaDe(string? configurada)
    {
        if (string.IsNullOrWhiteSpace(configurada))
            return Path.Combine(Path.GetTempPath(), "crm-saas-almacen");

        return Path.IsPathRooted(configurada)
            ? configurada
            : Path.Combine(Directory.GetCurrentDirectory(), configurada);
    }

    public async Task<string> UploadFileAsync(
        Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        var carpeta = CarpetaDe(_opciones.Carpeta);

        Directory.CreateDirectory(carpeta);

        var nombre = NombreSeguro(fileName);
        var destino = Path.Combine(carpeta, nombre);

        await using (var salida = File.Create(destino))
        {
            await fileStream.CopyToAsync(salida, ct);
        }

        _logger.LogInformation("Fichero guardado en disco: {Nombre}", nombre);

        return $"{_opciones.RutaPublica.TrimEnd('/')}/{Uri.EscapeDataString(nombre)}";
    }

    public Task DeleteFileAsync(string fileUrl, CancellationToken ct = default)
    {
        var nombre = Path.GetFileName(new Uri(fileUrl, UriKind.RelativeOrAbsolute).ToString());
        var carpeta = CarpetaDe(_opciones.Carpeta);

        var destino = Path.Combine(carpeta, Uri.UnescapeDataString(nombre));

        // Se comprueba que el fichero está dentro de la carpeta antes de borrarlo. Sin esto, una
        // dirección con «..» dentro borraría cualquier cosa a la que llegue el proceso.
        if (destino.StartsWith(carpeta, StringComparison.Ordinal) && File.Exists(destino))
            File.Delete(destino);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Un nombre que no puede escaparse de la carpeta ni pisar a otro.
    ///
    /// Se descarta la ruta que traiga el cliente y se conserva sólo el nombre; delante va un
    /// identificador, así que dos personas subiendo «captura.png» no se pisan.
    /// </summary>
    private static string NombreSeguro(string fileName)
    {
        var soloNombre = Path.GetFileName(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(soloNombre)) soloNombre = "fichero";

        foreach (var prohibido in Path.GetInvalidFileNameChars())
            soloNombre = soloNombre.Replace(prohibido, '-');

        if (soloNombre.Length > 100) soloNombre = soloNombre[^100..];

        return $"{Guid.NewGuid():N}-{soloNombre}";
    }
}
