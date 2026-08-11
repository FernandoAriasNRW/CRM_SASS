using BuildingBlocks.Application.Abstractions;
using Ticketing.Application.DTOs;

namespace Ticketing.Application.Queries;

public sealed record GetTicketByIdQuery(Guid TenantId, Guid TicketId) : IQuery<TicketDto?>;
