using Notifications.Domain.Entities;

namespace Notifications.Application.DTOs;

public sealed record NotificationDto(
    Guid Id,
    Guid TenantId,
    Guid RecipientUserId,
    Guid? SenderUserId,
    string Type,
    string Status,
    string Subject,
    string Body,
    string? Metadata,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? ReadAt)
{
    /// <summary>
    /// Mapea una entidad Notification a un DTO de forma segura.
    /// </summary>
    public static NotificationDto FromEntity(Notification notification)
    {
        return new NotificationDto(
            notification.Id,
            notification.TenantId,
            notification.RecipientUserId,
            notification.SenderUserId,
            notification.TypeValue,
            notification.StatusValue,
            notification.Subject,
            notification.Body,
            notification.Metadata,
            notification.CreatedAt,
            notification.SentAt,
            notification.ReadAt
        );
    }
}