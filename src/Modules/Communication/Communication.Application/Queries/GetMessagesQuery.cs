using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Communication.Application.DTOs;

namespace Communication.Application.Queries;

public sealed record GetMessagesQuery(
    Guid TenantId,
    Guid ConversationId,
    PaginationRequest Pagination
) : IQuery<PagedResult<MessageDto>>;
