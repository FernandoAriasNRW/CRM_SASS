using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Microsoft.Extensions.Logging;
using Ticketing.Application.Abstractions;

namespace Ticketing.Application.Intake;

/// <summary>
/// Quita un adjunto. Antes no había forma: una captura subida por error, o con datos que no
/// debían estar ahí, se quedaba en el ticket para siempre.
///
/// Primero se borra la fila y después el fichero. Al revés, si fallara el guardado, el ticket
/// enseñaría un adjunto cuyo enlace ya no lleva a nada; así, lo peor que puede pasar es un
/// fichero suelto en el almacenamiento, que no ve nadie.
/// </summary>
public sealed class RemoveTicketAttachmentHandler(
    IUserContext user,
    ITicketAttachmentRepository attachments,
    IStorageService storage,
    ITicketingUnitOfWork unitOfWork,
    ILogger<RemoveTicketAttachmentHandler> logger) : ICommandHandler<RemoveTicketAttachmentCommand, bool>
{
    public async Task<Result<bool>> Handle(RemoveTicketAttachmentCommand request, CancellationToken ct)
    {
        var attachment = await attachments.GetAsync(user.TenantId, request.TicketId, request.AttachmentId, ct);
        if (attachment is null)
            return Result<bool>.Failure(IntakeErrors.AttachmentNotFound);

        attachments.Remove(attachment);
        await unitOfWork.SaveChangesAsync(ct);

        try
        {
            await storage.DeleteFileAsync(attachment.Url);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "El adjunto {AttachmentId} se quitó del ticket pero su fichero sigue en el almacenamiento", attachment.Id);
        }

        return Result<bool>.Success(true);
    }
}
