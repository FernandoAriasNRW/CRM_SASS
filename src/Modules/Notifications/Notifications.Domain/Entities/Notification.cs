using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;
using Notifications.Domain.Events;
using Notifications.Domain.ValueObjects;

namespace Notifications.Domain.Entities;

/// <summary>
/// Entidad de dominio Notification.
/// </summary>
public sealed class Notification : AggregateRoot, ITenantEntity, ISoftDeletable
{
  public Guid TenantId { get; private set; }
  public Guid RecipientUserId { get; private set; }
  public Guid? SenderUserId { get; private set; }
  public string TypeValue { get; private set; } = string.Empty;
  public string StatusValue { get; private set; } = string.Empty;
  public string Subject { get; private set; } = string.Empty;
  public string Body { get; private set; } = string.Empty;
  public string? Metadata { get; private set; }

  /// <summary>
  /// De qué trata el aviso: uno de <see cref="NotificationCatalog"/>. Vacío en los anteriores a que
  /// existiera, que sólo guardaban el canal.
  /// </summary>
  public string? Kind { get; private set; }

  /// <summary>
  /// A qué cosa se refiere —una tarea, un ticket, un proyecto— para que el aviso lleve a ella.
  /// Un aviso que dice «te asignaron una tarea» y no deja abrirla obliga a buscarla a mano.
  /// </summary>
  public string? EntityType { get; private set; }
  public Guid? EntityId { get; private set; }
  public DateTime CreatedAt { get; private set; }
  public DateTime? SentAt { get; private set; }
  public DateTime? ReadAt { get; private set; }

  // Soft Delete
  public bool IsDeleted { get; private set; }

  public DateTime? DeletedAt { get; private set; }
  public Guid? DeletedBy { get; private set; }

  public NotificationType Type => NotificationType.FromName<NotificationType>(TypeValue)!;
  public NotificationStatus Status => NotificationStatus.FromName<NotificationStatus>(StatusValue)!;

  private Notification()
  { }

  // Constructor para uso del repositorio
  public Notification(
      DateTime nowUtc,
      Guid tenantId,
      Guid recipientUserId,
      string type,
      string subject,
      string body,
      Guid? senderUserId = null)
  {
    Id = Guid.NewGuid();
    TenantId = tenantId;
    RecipientUserId = recipientUserId;
    SenderUserId = senderUserId;
    TypeValue = type;
    StatusValue = NotificationStatus.Pending.Name;
    Subject = subject;
    Body = body;
    CreatedAt = nowUtc;
    IsDeleted = false;
    DeletedAt = null;
    DeletedBy = null;
  }

  public static Result<Notification> Create(
      DateTime nowUtc,
      Guid tenantId,
      Guid recipientUserId,
      string type,
      string subject,
      string body,
      Guid? senderUserId = null,
      string? kind = null,
      string? entityType = null,
      Guid? entityId = null)
  {
    var contentResult = NotificationContent.Create(subject, body);
    if (contentResult.IsFailure)
      return Result<Notification>.Failure(contentResult.Error!);

    var notification = new Notification
    {
      Id = Guid.NewGuid(),
      TenantId = tenantId,
      RecipientUserId = recipientUserId,
      SenderUserId = senderUserId,
      TypeValue = type,
      StatusValue = NotificationStatus.Pending.Name,
      Subject = subject,
      Body = body,
      Kind = kind,
      EntityType = entityType,
      EntityId = entityId,
      CreatedAt = nowUtc,
      IsDeleted = false,
      DeletedAt = null,
      DeletedBy = null
    };

    notification.RaiseDomainEvent(new NotificationCreatedEvent(notification.Id, tenantId, recipientUserId));

    return Result<Notification>.Success(notification);
  }

  public void MarkAsSent(DateTime nowUtc)
  {
    if (IsDeleted)
      throw new InvalidOperationException("No se puede marcar como enviada una notificación eliminada");

    StatusValue = NotificationStatus.Sent.Name;
    SentAt = nowUtc;
    RaiseDomainEvent(new NotificationSentEvent(Id, TenantId));
  }

  public void MarkAsRead(DateTime nowUtc)
  {
    if (IsDeleted)
      throw new InvalidOperationException("No se puede marcar como leída una notificación eliminada");

    StatusValue = NotificationStatus.Read.Name;
    ReadAt = nowUtc;
    RaiseDomainEvent(new NotificationReadEvent(Id, TenantId, RecipientUserId));
  }

  public void MarkAsFailed(string reason)
  {
    if (IsDeleted)
      throw new InvalidOperationException("No se puede marcar como fallida una notificación eliminada");

    StatusValue = NotificationStatus.Failed.Name;
    Metadata = reason;
    RaiseDomainEvent(new NotificationFailedEvent(Id, TenantId, reason));
  }

  /// <summary>
  /// Soft delete de la notificación.
  /// </summary>
  public void Delete(DateTime nowUtc, Guid deletedBy)
  {
    if (IsDeleted)
      throw new InvalidOperationException("La notificación ya ha sido eliminada");

    IsDeleted = true;
    DeletedAt = nowUtc;
    DeletedBy = deletedBy;

    RaiseDomainEvent(new NotificationDeletedEvent(Id, TenantId, deletedBy));
  }

  public void UpdateContent(string? subject, string? body, string? metadata, string type)
  {
    if (IsDeleted)
      throw new InvalidOperationException("No se puede actualizar el contenido de una notificación eliminada");

    if (string.IsNullOrWhiteSpace(subject))
      subject = Subject;

    if (string.IsNullOrWhiteSpace(body))
      body = Body;

    var contentResult = NotificationContent.Create(subject, body);

    if (contentResult.IsFailure)
      throw new InvalidOperationException(contentResult.Error);

    Subject = subject;
    Body = body;
    Metadata = metadata;
    TypeValue = type;
    RaiseDomainEvent(new NotificationUpdatedEvent(Id, TenantId));
  }

  /// <summary>
  /// Restaura una notificación eliminada.
  /// </summary>
  public void Restore()
  {
    if (!IsDeleted)
      throw new InvalidOperationException("La notificación no está eliminada");

    IsDeleted = false;
    DeletedAt = null;
    DeletedBy = null;
  }
}