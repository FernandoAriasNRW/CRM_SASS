namespace BuildingBlocks.Infrastructure.Storage;

/// <summary>Dónde se guardan los ficheros y con qué dirección se sirven.</summary>
public sealed class DiskStorageOptions
{
    /// <summary>La sección de configuración: <c>DiskStorage__Folder</c> en variables de entorno.</summary>
    public const string SectionName = "DiskStorage";

    /// <summary>
    /// La ruta con la que se publicaba antes de pasar los nombres a inglés. Se sigue sirviendo porque
    /// hay documentos guardados con imágenes enlazadas a <c>/almacen/…</c>, y cambiarla sin más las
    /// dejaría rotas.
    /// </summary>
    public const string LegacyPublicPath = "/almacen";

    /// <summary>
    /// Carpeta del servidor. Vacía significa la de por defecto, dentro del temporal del sistema.
    ///
    /// El valor por defecto **no** cuelga del directorio de la aplicación: la imagen de Docker
    /// corre con un usuario sin privilegios y `/app` no es escribible, así que crear la carpeta
    /// ahí tiraba el arranque entero.
    /// </summary>
    public string Folder { get; set; } = string.Empty;

    /// <summary>Prefijo con el que se publican. Tiene que coincidir con el de los ficheros estáticos.</summary>
    public string PublicPath { get; set; } = "/storage";
}
