using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using MediatR;
using Microsoft.Extensions.Logging;
using System.IO;

namespace Docs.Application.Handlers.Commands;

public record UploadFileCommand(Stream FileStream, string FileName, string ContentType) : IRequest<Result<string>>;

public class UploadFileHandler(IStorageService storageService, ILogger<UploadFileHandler> logger)
    : IRequestHandler<UploadFileCommand, Result<string>>
{
    public async Task<Result<string>> Handle(UploadFileCommand request, CancellationToken cancellationToken)
    {
        if (request.FileStream == null || request.FileStream.Length == 0)
        {
            return Result<string>.Failure("El fichero está vacío.");
        }

        try
        {
            var url = await storageService.UploadFileAsync(
                request.FileStream, request.FileName, request.ContentType, cancellationToken);

            return Result<string>.Success(url);
        }
        catch (Exception ex)
        {
            // El almacenamiento puede fallar por motivos que no son culpa de quien sube: unas
            // credenciales sin permiso de escritura, un disco lleno, la red. Antes la excepción
            // subía hasta el manejador global y la pantalla recibía un 500 con la traza dentro;
            // ahora es un 400 con un motivo, y la traza se queda en el registro del servidor.
            logger.LogError(ex, "No se pudo guardar {Fichero} en el almacenamiento", request.FileName);
            return Result<string>.Failure(
                "No se pudo guardar el fichero. Revisa la configuración del almacenamiento.");
        }
    }
}
