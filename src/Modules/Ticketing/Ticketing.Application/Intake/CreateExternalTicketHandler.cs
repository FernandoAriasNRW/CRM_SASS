using System.Net.Mail;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Abstractions.Repositories;
using Ticketing.Domain.Entities;
using Ticketing.Domain.ValueObjects;

namespace Ticketing.Application.Intake;

public sealed class CreateExternalTicketHandler(
    IIntakeKeyRepository keys,
    ITicketRepository tickets,
    ITicketAttachmentRepository attachments,
    IStorageService storage,
    ITicketingUnitOfWork unitOfWork) : ICommandHandler<CreateExternalTicketCommand, ExternalTicketCreatedDto>
{
    private static readonly (Func<CreateExternalTicketCommand, string?> Field, string Name)[] Required =
    [
        (r => r.Title, "title"),
        (r => r.Description, "description"),
        (r => r.RequesterName, "requesterName"),
        (r => r.RequesterEmail, "requesterEmail"),
        (r => r.RequesterPhone, "requesterPhone"),
        (r => r.RequesterCompany, "requesterCompany"),
    ];

    public async Task<Result<ExternalTicketCreatedDto>> Handle(CreateExternalTicketCommand request, CancellationToken ct)
    {
        // La clave va primero: a quien no la tiene no se le dice nada sobre qué campos faltan.
        if (string.IsNullOrWhiteSpace(request.Key))
            return Failure(IntakeErrors.InvalidKey);

        var key = await keys.FindActiveByHashAsync(IntakeKey.HashOf(request.Key.Trim()), ct);
        if (key is null)
            return Failure(IntakeErrors.InvalidKey);

        // Todos los que faltan a la vez, no el primero: quien integra un formulario arregla la
        // lista entera de una pasada en vez de descubrirla campo a campo.
        var missing = Required.Where(o => string.IsNullOrWhiteSpace(o.Field(request))).Select(o => o.Name).ToList();
        if (missing.Count > 0)
            return Failure("Faltan campos obligatorios: " + string.Join(", ", missing));

        if (!IsEmail(request.RequesterEmail!.Trim()))
            return Failure("requesterEmail no es un email válido");

        var tooLong = new (string? Value, int Max, string Name)[]
        {
            (request.RequesterName, 200, "requesterName"),
            (request.RequesterEmail, 320, "requesterEmail"),
            (request.RequesterPhone, 40, "requesterPhone"),
            (request.RequesterCompany, 200, "requesterCompany"),
            (request.Classification, 100, "classification"),
        }.FirstOrDefault(c => c.Value is not null && c.Value.Trim().Length > c.Max);
        if (tooLong.Name is not null)
            return Failure($"{tooLong.Name} admite hasta {tooLong.Max} caracteres");

        // Sin prioridad, la media: un formulario de cliente no suele preguntarla.
        var priority = string.IsNullOrWhiteSpace(request.Priority)
            ? TicketPriority.Medium
            : TicketPriority.FromName<TicketPriority>(request.Priority.Trim());
        if (priority is null)
            return Failure("priority no es válida: Low, Medium, High o Critical");

        TicketStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            status = TicketStatus.FromName<TicketStatus>(request.Status.Trim());
            if (status is null)
                return Failure("status no es válido: Open, InProgress, PendingInfo, Resolved o Closed");
        }

        if (request.Tags.Count > 20 || request.Tags.Any(t => t.Trim().Length > 40))
            return Failure("Se admiten hasta 20 etiquetas de hasta 40 caracteres");

        var rejection = AttachmentStorage.RejectionReason(request.Attachments);
        if (rejection is not null)
            return Failure(rejection);

        var created = Ticket.CreateFromExternal(key, new ExternalTicketRequest(
            request.Title!.Trim(), request.Description!.Trim(), priority, status,
            request.RequesterName, request.RequesterEmail, request.RequesterPhone, request.RequesterCompany,
            request.Classification, request.TeamId, request.Tags));
        if (created.IsFailure)
            return Failure(created.Error!);

        var ticket = created.Value!;
        var (uploaded, uploadError) = await AttachmentStorage.UploadAsync(storage, ticket, request.Attachments, uploadedBy: null, ct);
        if (uploadError is not null)
            return Failure(uploadError);

        try
        {
            key.MarkUsed(DateTime.UtcNow);
            await tickets.AddAsync(ticket, ct);
            foreach (var attachment in uploaded)
                await attachments.AddAsync(attachment, ct);

            await unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            await AttachmentStorage.RollbackAsync(storage, uploaded);
            throw;
        }

        return Result<ExternalTicketCreatedDto>.Success(new(ticket.Id, ticket.Status.Name, ticket.CreatedAt, uploaded.Count));
    }

    private static Result<ExternalTicketCreatedDto> Failure(string error) => Result<ExternalTicketCreatedDto>.Failure(error);

    private static bool IsEmail(string email)
        => MailAddress.TryCreate(email, out var address) && address.Address == email;
}
