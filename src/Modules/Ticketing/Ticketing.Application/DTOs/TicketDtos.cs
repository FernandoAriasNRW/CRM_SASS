using Ticketing.Domain.Entities;

namespace Ticketing.Application.DTOs;

public sealed record TicketDto(
    Guid Id,
    Guid TenantId,
    Guid CustomerId,
    Guid? AssignedAgentId,
    string Title,
    string Description,
    string Priority,
    string Status,
    DateTime CreatedAt,
    DateTime? ResolvedAt
)
{
  internal static TicketDto? FromEntity(Ticket ticket)
  {
    return ticket is null ? null : new(
      ticket.Id,
      ticket.TenantId,
      ticket.CustomerId,
      ticket.AssignedAgentId,
      ticket.Title,
      ticket.Description,
      ticket.Priority.ToString(),
      ticket.Status.ToString(),
      ticket.CreatedAt,
      ticket.ResolvedAt
    );
  }
}




