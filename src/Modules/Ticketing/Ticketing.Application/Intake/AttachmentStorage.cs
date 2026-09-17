using System.Net.Mail;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Abstractions.Repositories;
using Ticketing.Domain.Entities;
using Ticketing.Domain.ValueObjects;

namespace Ticketing.Application.Intake;

/// <summary>Guardar adjuntos, igual desde fuera que desde la ficha.</summary>
internal static class AttachmentStorage
{
    public static string? RejectionReason(IReadOnlyList<IncomingFile> files)
    {
        if (files.Count > AttachmentRules.MaxFiles)
            return $"Se admiten como mucho {AttachmentRules.MaxFiles} adjuntos";

        return files
            .Select(f => AttachmentRules.RejectionReason(f.Name, f.ContentType, f.Size))
            .FirstOrDefault(r => r is not null);
    }

    /// <summary>
    /// Sube los ficheros y devuelve sus adjuntos, o el motivo por el que uno no se pudo guardar.
    /// Si uno falla, borra los que ya subió: un ticket que no se crea no debe dejar ficheros
    /// sueltos en el almacenamiento.
    ///
    /// <b>Un fichero que el almacenamiento rechaza es culpa del fichero, no del servidor.</b>
    /// Salió probando desde el navegador: un vídeo que no era vídeo lo rechazaba Cloudinary y la
    /// entrada respondía 500, así que la web del cliente no podía decir qué adjunto quitar.
    /// </summary>
    public static async Task<(List<TicketAttachment> Uploaded, string? Error)> UploadAsync(
        IStorageService storage, Ticket ticket, IReadOnlyList<IncomingFile> files, Guid? uploadedBy, CancellationToken ct)
    {
        var uploaded = new List<TicketAttachment>();
        foreach (var file in files)
        {
            string url;
            try
            {
                await using var content = file.Open();
                url = await storage.UploadFileAsync(content, file.Name, file.ContentType, ct);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                await RollbackAsync(storage, uploaded);
                return (uploaded, $"No se pudo guardar «{file.Name}»: comprueba que es una imagen o un vídeo válido");
            }

            uploaded.Add(TicketAttachment.Create(ticket, file.Name, url, file.ContentType, file.Size, uploadedBy, DateTime.UtcNow));
        }
        return (uploaded, null);
    }

    public static async Task RollbackAsync(IStorageService storage, IEnumerable<TicketAttachment> uploaded)
    {
        foreach (var attachment in uploaded)
        {
            try { await storage.DeleteFileAsync(attachment.Url); }
            catch { /* Lo importante es el error original; un fichero huérfano no lo tapa. */ }
        }
    }

    public static TicketAttachmentDto ToDto(TicketAttachment a)
        => new(a.Id, a.Name, a.Url, a.ContentType, a.Size, a.UploadedAtUtc, a.UploadedBy is null);
}
