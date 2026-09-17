using Ticketing.Domain.ValueObjects;

namespace Ticketing.Domain.Entities;

/// <summary>Lo que manda un cliente de la organización al abrir un ticket desde fuera.</summary>
public sealed record ExternalTicketRequest(
    string Title,
    string Description,
    TicketPriority Priority,
    TicketStatus? Status,
    string? RequesterName,
    string? RequesterEmail,
    string? RequesterPhone,
    string? RequesterCompany,
    string? Classification,
    Guid? TeamId,
    IReadOnlyList<string> Tags);
