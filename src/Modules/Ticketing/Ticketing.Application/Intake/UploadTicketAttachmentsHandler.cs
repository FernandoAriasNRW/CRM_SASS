using System.Net.Mail;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Abstractions.Repositories;
using Ticketing.Domain.Entities;
using Ticketing.Domain.ValueObjects;

namespace Ticketing.Application.Intake;

public sealed class UploadTicketAttachmentsHandler(
    IUserContext user,
    ITicketRepository tickets,
    ITicketAttachmentRepository attachments,
    IStorageService storage,
    ITicketingUnitOfWork unitOfWork) : ICommandHandler<UploadTicketAttachmentsCommand, List<TicketAttachmentDto>>
{
    public async Task<Result<List<TicketAttachmentDto>>> Handle(UploadTicketAttachmentsCommand request, CancellationToken ct)
    {
        if (request.Attachments.Count == 0)
            return Result<List<TicketAttachmentDto>>.Failure("No llegó ningún fichero");

        var rejection = AttachmentStorage.RejectionReason(request.Attachments);
        if (rejection is not null)
            return Result<List<TicketAttachmentDto>>.Failure(rejection);

        var ticket = await tickets.GetByIdAsync(user.TenantId, request.TicketId, ct);
        if (ticket is null)
            return Result<List<TicketAttachmentDto>>.Failure(IntakeErrors.TicketNotFound);

        var (uploaded, uploadError) = await AttachmentStorage.UploadAsync(storage, ticket, request.Attachments, request.UploadedBy, ct);
        if (uploadError is not null)
            return Result<List<TicketAttachmentDto>>.Failure(uploadError);
        try
        {
            foreach (var attachment in uploaded)
                await attachments.AddAsync(attachment, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            await AttachmentStorage.RollbackAsync(storage, uploaded);
            throw;
        }

        return Result<List<TicketAttachmentDto>>.Success(uploaded.Select(AttachmentStorage.ToDto).ToList());
    }
}
