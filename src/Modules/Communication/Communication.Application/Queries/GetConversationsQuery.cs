using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Communication.Application.DTOs;

namespace Communication.Application.Queries;

public sealed record GetConversationsQuery(
    Guid TenantId,
    string? Type,
    PaginationRequest Pagination
) : IQuery<PagedResult<ConversationDto>>;
