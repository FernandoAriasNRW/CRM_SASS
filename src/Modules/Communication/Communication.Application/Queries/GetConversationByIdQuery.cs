using BuildingBlocks.Application.Abstractions;
using Communication.Application.DTOs;

namespace Communication.Application.Queries;

public sealed record GetConversationByIdQuery(
    Guid TenantId,
    Guid Id
) : IQuery<ConversationDto?>;
