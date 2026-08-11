using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using MediatR;
using System.IO;

namespace Docs.Application.Handlers.Commands;

public record UploadFileCommand(Stream FileStream, string FileName, string ContentType) : IRequest<Result<string>>;

public class UploadFileHandler(IStorageService storageService) 
    : IRequestHandler<UploadFileCommand, Result<string>>
{
    public async Task<Result<string>> Handle(UploadFileCommand request, CancellationToken cancellationToken)
    {
        if (request.FileStream == null || request.FileStream.Length == 0)
        {
            return Result<string>.Failure("The file is empty.");
        }

        var url = await storageService.UploadFileAsync(request.FileStream, request.FileName, request.ContentType, cancellationToken);
        
        return Result<string>.Success(url);
    }
}
