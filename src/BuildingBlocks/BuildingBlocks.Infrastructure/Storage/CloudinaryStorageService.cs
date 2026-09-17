using BuildingBlocks.Application.Abstractions;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Infrastructure.Storage;

public class CloudinaryStorageService : IStorageService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryStorageService> _logger;

    public CloudinaryStorageService(IOptions<CloudinaryOptions> options, ILogger<CloudinaryStorageService> logger)
    {
        _logger = logger;
        
        var acc = new Account(
            options.Value.CloudName,
            options.Value.ApiKey,
            options.Value.ApiSecret
        );

        _cloudinary = new Cloudinary(acc);
        _cloudinary.Api.Secure = true;
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        _logger.LogInformation("Uploading file {FileName} to Cloudinary", fileName);
        
        // Las imágenes van por el endpoint de imágenes y todo lo demás por el de ficheros en
        // bruto. Antes todo subía como imagen, así que un PDF o un CSV los rechazaba Cloudinary:
        // como nunca hubo credenciales configuradas, eso no lo había sufrido nadie todavía.
        var isImage = contentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;
        var isVideo = contentType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true;

        var file = new FileDescription(fileName, fileStream);

        // Dos `await` y no un ternario: el ternario obliga a los dos lados al mismo tipo y el
        // compilador elige el de la izquierda, así que un `RawUploadParams` acababa donde se
        // esperaba un `ImageUploadParams`.
        RawUploadResult uploadResult;
        if (isImage)
            uploadResult = await _cloudinary.UploadAsync(
                new ImageUploadParams { File = file, Folder = "crm-saas-suite" }, ct);
        else if (isVideo)
            // Los vídeos por su propio endpoint: como fichero en bruto el límite de tamaño es
            // mucho menor, y una grabación de pantalla de un minuto ya no cabría.
            uploadResult = await _cloudinary.UploadLargeAsync(
                new VideoUploadParams { File = file, Folder = "crm-saas-suite" }, cancellationToken: ct);
        else
            // «raw» es el tipo de recurso: es lo que hace que Cloudinary acepte un PDF o un CSV
            // sin intentar tratarlos como imagen.
            uploadResult = await _cloudinary.UploadAsync(
                new RawUploadParams { File = file, Folder = "crm-saas-suite" }, "raw", ct);
        
        if (uploadResult.Error != null)
        {
            _logger.LogError("Cloudinary upload failed: {Error}", uploadResult.Error.Message);
            throw new Exception($"Cloudinary upload failed: {uploadResult.Error.Message}");
        }

        return uploadResult.SecureUrl.ToString();
    }

    public Task DeleteFileAsync(string fileUrl, CancellationToken ct = default)
    {
        // Simple implementation, parsing public id from URL would be needed for full delete support
        // But for this use case, upload is the primary concern
        _logger.LogWarning("DeleteFileAsync not fully implemented for Cloudinary URLs yet.");
        return Task.CompletedTask;
    }
}
