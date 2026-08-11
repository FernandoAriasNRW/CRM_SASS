using BuildingBlocks.Application.Abstractions;
using Notifications.Application.DTOs;

namespace Notifications.Application.Queries;

public sealed record GetNotificationByIdQuery(Guid TenantId, Guid Id) : IQuery<NotificationDto?>;
