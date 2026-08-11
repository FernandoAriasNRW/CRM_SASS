using BuildingBlocks.Application.Abstractions;

namespace Notifications.Application.Queries;

public sealed record GetUnreadCountQuery(Guid TenantId, Guid RecipientUserId) : IQuery<int>;
