using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Notifications.Domain.Entities;

namespace Notifications.Application.Preferences;

public sealed record GetNotificationPreferencesQuery(Guid TenantId, Guid UserId) : IQuery<NotificationPreferencesDto>;
