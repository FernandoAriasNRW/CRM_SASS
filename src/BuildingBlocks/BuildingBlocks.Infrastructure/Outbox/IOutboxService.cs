using BuildingBlocks.Domain.Primitives;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Outbox;

public interface IOutboxService
{
    Task AddMessageAsync(string eventType, string payload, CancellationToken ct = default);
}