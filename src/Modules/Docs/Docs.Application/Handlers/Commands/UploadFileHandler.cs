using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using MediatR;
using Microsoft.Extensions.Logging;
using System.IO;

namespace Docs.Application.Handlers.Commands;

/// <summary>
/// Sube un fichero para un documento: una imagen o un adjunto.
///
/// Pide poder editar documentos: antes lo podía hacer cualquiera con sesión, aunque su rol solo le
/// dejara leerlos.
/// </summary>
public record UploadFileCommand(Stream FileStream, string FileName, string ContentType) : IRequest<Result<string>>, IAuthorizeEntity
{
    public string EntityType => "Document";
    public Guid EntityId => Guid.Empty;
    public string RequiredPermission => "Write";
}

public class UploadFileHandler(IStorageService storageService, ILogger<UploadFileHandler> logger)
    : IRequestHandler<UploadFileCommand, Result<string>>
{
    public async Task<Result<string>> Handle(UploadFileCommand request, CancellationToken cancellationToken)
    {
        var rejection = UploadRules.RejectionReason(request.FileName, request.FileStream?.Length ?? 0);
        if (rejection is not null)
            return Result<string>.Failure(rejection);

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
