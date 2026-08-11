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
        
        var uploadParams = new ImageUploadParams()
        {
            File = new FileDescription(fileName, fileStream),
            Folder = "crm-saas-suite"
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams, ct);
        
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
