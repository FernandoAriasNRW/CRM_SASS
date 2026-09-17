using System.Net.Mail;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Abstractions.Repositories;
using Ticketing.Domain.Entities;
using Ticketing.Domain.ValueObjects;

namespace Ticketing.Application.Intake;

public sealed class GetTicketAttachmentsHandler(ITicketAttachmentRepository attachments)
    : IQueryHandler<GetTicketAttachmentsQuery, List<TicketAttachmentDto>>
{
    public async Task<Result<List<TicketAttachmentDto>>> Handle(GetTicketAttachmentsQuery request, CancellationToken ct)
    {
        var list = await attachments.GetByTicketAsync(request.TenantId, request.TicketId, ct);
        return Result<List<TicketAttachmentDto>>.Success(list.Select(AttachmentStorage.ToDto).ToList());
    }
}
