using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Communication.Application.Abstractions.Repositories;
using Communication.Application.DTOs;
using Communication.Application.Queries;

namespace Communication.Application.Handlers.Queries;

public sealed class GetConversationsHandler(IConversationRepository repository)
    : IQueryHandler<GetConversationsQuery, PagedResult<ConversationDto>>
{
    private readonly IConversationRepository _repository = repository;

    public async Task<Result<PagedResult<ConversationDto>>> Handle(GetConversationsQuery request, CancellationToken ct)
    {
        var (items, totalCount) = await _repository.GetByTenantAsync(request.TenantId, request.Type, request.Pagination, ct);
        var dtos = items.Select(ConversationDto.FromDomain).ToList();
        var result = PagedResult<ConversationDto>.Create(dtos, request.Pagination.Page, request.Pagination.PageSize, totalCount);
        return Result<PagedResult<ConversationDto>>.Success(result);
    }
}

public sealed class GetConversationByIdHandler(IConversationRepository repository)
    : IQueryHandler<GetConversationByIdQuery, ConversationDto?>
{
    private readonly IConversationRepository _repository = repository;

    public async Task<Result<ConversationDto?>> Handle(GetConversationByIdQuery request, CancellationToken ct)
    {
        var conversation = await _repository.GetByIdAsync(request.TenantId, request.Id, includedDeleted: false, ct);
        return Result<ConversationDto?>.Success(conversation is null ? null : ConversationDto.FromDomain(conversation));
    }
}

public sealed class GetMessagesHandler(IMessageRepository repository)
    : IQueryHandler<GetMessagesQuery, PagedResult<MessageDto>>
{
    private readonly IMessageRepository _repository = repository;

    public async Task<Result<PagedResult<MessageDto>>> Handle(GetMessagesQuery request, CancellationToken ct)
    {
        var (items, totalCount) = await _repository.GetByConversationAsync(request.TenantId, request.ConversationId, request.Pagination, ct);
        var dtos = items.Select(MessageDto.FromDomain).ToList();
        var result = PagedResult<MessageDto>.Create(dtos, request.Pagination.Page, request.Pagination.PageSize, totalCount);
        return Result<PagedResult<MessageDto>>.Success(result);
    }
}
