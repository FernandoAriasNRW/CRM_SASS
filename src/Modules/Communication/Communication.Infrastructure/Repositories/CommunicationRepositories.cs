using BuildingBlocks.Domain;
using Communication.Application.Abstractions.Repositories;
using Communication.Domain.Entities;
using Communication.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Communication.Infrastructure.Repositories;

public sealed class EfConversationRepository(CommunicationsDbContext context) : IConversationRepository
{
  private readonly CommunicationsDbContext _context = context;

  public async Task<(IReadOnlyList<Conversation> Items, int TotalCount)> GetByTenantAsync(
        Guid tenantId,
        string? type,
        PaginationRequest pagination,
        CancellationToken ct = default)
  {
    var query = _context.Conversations.Where(c => c.TenantId == tenantId);

    if (!string.IsNullOrEmpty(type))
      query = query.Where(c => c.TypeValue.ToString() == type);

    var totalCount = await query.CountAsync(ct);

    var items = await query
        .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
        .Skip(pagination.Skip)
        .Take(pagination.Take)
        .ToListAsync(ct);

    return (items, totalCount);
  }

  public async Task<Conversation?> GetByIdAsync(Guid tenantId, Guid id, bool includedDeleted, CancellationToken ct = default)
  {
    var query = _context.Conversations.AsQueryable();

    if (!includedDeleted)
      query = query.Where(c => !c.IsDeleted);

    return await query.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, ct);
  }

  public async Task<Conversation> AddAsync(Conversation conversation, CancellationToken ct = default)
  {
    _context.Conversations.Add(conversation);
    await _context.SaveChangesAsync(ct);
    return conversation;
  }

  public async Task UpdateAsync(object conversation, CancellationToken ct)
  {
    if (conversation is not Conversation entity)
      throw new ArgumentException("Expected a Conversation instance", nameof(conversation));

    _context.Conversations.Update(entity);
    await _context.SaveChangesAsync(ct);
  }
}

public sealed class EfMessageRepository(CommunicationsDbContext context) : IMessageRepository
{
  private readonly CommunicationsDbContext _context = context;

  public async Task<(IReadOnlyList<Message> Items, int TotalCount)> GetByConversationAsync(
      Guid tenantId,
      Guid conversationId,
      PaginationRequest pagination,
      CancellationToken ct = default)
  {
    var query = _context.Messages
        .Where(m => m.TenantId == tenantId && m.ConversationId == conversationId && !m.IsDeleted);

    var totalCount = await query.CountAsync(ct);

    var items = await query
        .OrderByDescending(m => m.SentAt)
        .Skip(pagination.Skip)
        .Take(pagination.Take)

        .ToListAsync(ct);

    return (items, totalCount);
  }

  //public async Task<Message?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
  //{
  //  var message = await _context.Messages
  //      .AsNoTracking()
  //      .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == id, ct);

  //  return message;
  //}

  public async Task<Message> AddAsync(Message message, CancellationToken ct = default)
  {
    _context.Messages.Add(message);
    await _context.SaveChangesAsync(ct);
    return message;
  }

  public async Task<bool> UpdateAsync(Message message, CancellationToken ct = default)
  {
    _context.Messages.Update(message);
    await _context.SaveChangesAsync(ct);
    return true;
  }

  public async Task<Message> GetByIdAsync(Guid tenantId, Guid messageId, bool includedDeleted, CancellationToken cancellationToken)
  {
    var query = _context.Messages.AsQueryable();

    if (!includedDeleted)
      query = query.Where(m => !m.IsDeleted);

    return (await query.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == messageId, cancellationToken))!;
  }
}
