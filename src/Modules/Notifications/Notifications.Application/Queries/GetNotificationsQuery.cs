using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Notifications.Application.DTOs;

namespace Notifications.Application.Queries;

public sealed record GetNotificationsQuery(
    Guid TenantId,
    Guid? RecipientId,
    string? Type,
    string? Status,
    PaginationRequest Pagination
) : IQuery<PagedResult<NotificationDto>>;
