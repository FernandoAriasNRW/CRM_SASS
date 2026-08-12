using BuildingBlocks.Application.Abstractions;
using Notifications.Application.Abstractions;
using BuildingBlocks.Domain;
using Notifications.Application.Abstractions.Repositories;
using Notifications.Application.Commands;
using Notifications.Domain.Entities;

namespace Notifications.Application.Handlers.Commands;

public sealed class CreateNotificationHandler(
    INotificationRepository repository,
    INotificationsUnitOfWork unitOfWork) : ICommandHandler<CreateNotificationCommand, Notification>
{
  private readonly INotificationRepository _repository = repository;
  private readonly INotificationsUnitOfWork _unitOfWork = unitOfWork;

  public async Task<Result<Notification>> Handle(CreateNotificationCommand request, CancellationToken ct)
  {
    var notificationResult = Notification.Create(
        request.TenantId,
        request.RecipientUserId,
        request.Type,
        request.Subject,
        request.Body,
        request.SenderUserId);

    if (notificationResult.IsFailure)
      return Result<Notification>.Failure(notificationResult.Error!);

    var notification = notificationResult.Value;

    if (notification == null)
      return Result<Notification>.Failure("Error al crear la notificación");

    await _repository.AddAsync(notification, ct);
    await _unitOfWork.SaveChangesAsync(ct);

    return Result<Notification>.Success(notification);
  }
}

public sealed class MarkNotificationAsReadHandler(
    INotificationRepository repository,
    INotificationsUnitOfWork unitOfWork) : ICommandHandler<MarkNotificationAsReadCommand, bool>
{
  private readonly INotificationRepository _repository = repository;
  private readonly INotificationsUnitOfWork _unitOfWork = unitOfWork;

  public async Task<Result<bool>> Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
  {
    var dto = await _repository.GetByIdAsync(request.TenantId, request.NotificationId, includeDeleted: false, ct: cancellationToken);
    if (dto == null) return Result<bool>.Failure("Notification not found");

    if (dto.IsDeleted)
      return Result<bool>.Failure("No se puede modificar una notificación eliminada");

    dto.MarkAsRead();

    var updated = await _repository.UpdateAsync(dto, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    return Result<bool>.Success(updated);
  }
}

public sealed class DeleteNotificationHandler(
    INotificationRepository repository) : ICommandHandler<DeleteNotificationCommand, bool>
{
  private readonly INotificationRepository _repository = repository;

  public async Task<Result<bool>> Handle(DeleteNotificationCommand request, CancellationToken cancellationToken)
  {
    var existing = await _repository.GetByIdAsync(request.TenantId, request.NotificationId, includeDeleted: false, ct: cancellationToken);
    if (existing == null)
      return Result<bool>.Failure("Notificación no encontrada");

    if (existing.IsDeleted)
      return Result<bool>.Failure("La notificación ya ha sido eliminada");

    await _repository.DeleteAsync(request.TenantId, request.NotificationId, request.DeletedBy, cancellationToken);
    return Result<bool>.Success(true);
  }
}

public sealed class RestoreNotificationHandler(
    INotificationRepository repository) : ICommandHandler<RestoreNotificationCommand, bool>
{
  private readonly INotificationRepository _repository = repository;

  public async Task<Result<bool>> Handle(RestoreNotificationCommand request, CancellationToken cancellationToken)
  {
    var existing = await _repository.GetByIdAsync(request.TenantId, request.NotificationId, includeDeleted: true, ct: cancellationToken);
    if (existing == null)
      return Result<bool>.Failure("Notificación no encontrada");

    if (!existing.IsDeleted)
      return Result<bool>.Failure("La notificación no está eliminada");

    await _repository.RestoreAsync(request.TenantId, request.NotificationId, cancellationToken);
    return Result<bool>.Success(true);
  }
}