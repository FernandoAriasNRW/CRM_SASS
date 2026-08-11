using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BuildingBlocks.Application.Abstractions;

public interface IStorageService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default);
    Task DeleteFileAsync(string fileUrl, CancellationToken ct = default);
}
