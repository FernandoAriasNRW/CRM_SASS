using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Notifications.Application.Abstractions.Queries;
using Notifications.Application.Abstractions.Repositories;
using Notifications.Application.DTOs;
using Notifications.Application.Queries;

namespace Notifications.Application.Handlers.Queries;

public sealed class GetNotificationsHandler(INotificationQueries queries)
    : IQueryHandler<GetNotificationsQuery, PagedResult<NotificationDto>>
{
    public async Task<Result<PagedResult<NotificationDto>>> Handle(GetNotificationsQuery request, CancellationToken ct)
    {
        var result = await queries.GetByTenantAsync(
            request.TenantId, request.RecipientId, request.Type, request.Status,
            request.Pagination.Page, request.Pagination.PageSize, ct);

        return Result<PagedResult<NotificationDto>>.Success(result);
    }
}

public sealed class GetNotificationByIdHandler(INotificationQueries queries)
    : IQueryHandler<GetNotificationByIdQuery, NotificationDto?>
{
    public async Task<Result<NotificationDto?>> Handle(GetNotificationByIdQuery request, CancellationToken ct)
    {
        var dto = await queries.GetByIdAsync(request.TenantId, request.Id, ct);
        return Result<NotificationDto?>.Success(dto);
    }
}

public sealed class GetUnreadCountHandler(INotificationQueries queries)
    : IQueryHandler<GetUnreadCountQuery, int>
{
    public async Task<Result<int>> Handle(GetUnreadCountQuery request, CancellationToken ct)
    {
        var count = await queries.GetUnreadCountAsync(request.TenantId, request.RecipientUserId, ct);
        return Result<int>.Success(count);
    }
}
