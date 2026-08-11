using BuildingBlocks.Domain;
using Communication.Domain.Entities;

namespace Communication.Application.Abstractions.Repositories;

public interface IConversationRepository
{
  Task<(IReadOnlyList<Conversation> Items, int TotalCount)> GetByTenantAsync(
      Guid tenantId,
      string? type,
      PaginationRequest pagination,
      CancellationToken ct = default);

  Task<Conversation?> GetByIdAsync(Guid tenantId, Guid conversationId, bool includedDeleted, CancellationToken ct = default);

  Task<Conversation> AddAsync(Conversation conversation, CancellationToken ct = default);

  Task UpdateAsync(object conversation, CancellationToken ct);
}

public interface IMessageRepository
{
  Task<(IReadOnlyList<Message> Items, int TotalCount)> GetByConversationAsync(
      Guid tenantId,
      Guid conversationId,
      PaginationRequest pagination,
      CancellationToken ct = default);

  Task<Message> AddAsync(Message message, CancellationToken ct = default);

  Task<bool> UpdateAsync(Message message, CancellationToken ct = default);

  Task<Message> GetByIdAsync(Guid tenantId, Guid messageId, bool includedDeleted, CancellationToken cancellationToken);
}