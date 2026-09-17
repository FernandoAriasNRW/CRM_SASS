using BuildingBlocks.Infrastructure.Storage;
using Microsoft.Extensions.FileProviders;

namespace ApiHost.Startup;

/// <summary>
/// Los ficheros que se suben a los documentos, cuando se guardan en disco.
///
/// Va detrás de CORS y **delante de la autenticación**: son las imágenes y los adjuntos que el
/// navegador pide directamente desde el <c>&lt;img&gt;</c> o el enlace del documento, sin cabecera de
/// sesión. Exigirla aquí dejaría todas las imágenes rotas dentro del editor, que es el mismo fallo
/// que tenía «Exportar HTML» al abrirse en una pestaña nueva.
/// </summary>
public static class DiskStorageFiles
{
    public static void UseDiskStorageFiles(this WebApplication app)
    {
        var physicalPath = DiskStorageService.ResolveFolder(
            app.Configuration[$"{DiskStorageOptions.SectionName}:Folder"]);

        var publicPath = app.Configuration[$"{DiskStorageOptions.SectionName}:PublicPath"] ?? "/storage";

        // **No puede tumbar el arranque.** La primera versión hacía `CreateDirectory` a secas sobre una
        // ruta relativa a `/app`, que en el contenedor no es escribible porque la imagen corre con
        // usuario sin privilegios: la API se caía entera al arrancar con «Access to the path
        // '/app/almacen' is denied». Que no se puedan subir ficheros es un problema; que no arranque el
        // servidor es otro mucho mayor.
        try
        {
            Directory.CreateDirectory(physicalPath);

            // La ruta actual y la de antes del cambio a inglés, sobre la misma carpeta: los documentos
            // guardados con imágenes en `/almacen/…` tienen que seguir viéndose.
            foreach (var requestPath in new[] { publicPath, DiskStorageOptions.LegacyPublicPath }.Distinct())
            {
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(physicalPath),
                    RequestPath = requestPath.TrimEnd('/'),

                    // Sin esto, un `.md` o un `.csv` subidos se sirven como 404: el proveedor por
                    // defecto sólo conoce los tipos que trae en su tabla.
                    ServeUnknownFileTypes = true,
                    DefaultContentType = "application/octet-stream"
                });
            }
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex,
                "No se pudo preparar la carpeta de ficheros {Path}: subir adjuntos no funcionará", physicalPath);
        }
    }
}
