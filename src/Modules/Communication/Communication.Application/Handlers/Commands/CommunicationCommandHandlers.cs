using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Communication.Application.Abstractions.Repositories;
using Communication.Application.Commands;
using Communication.Application.DTOs;
using Communication.Domain.Entities;
using Communication.Domain.ValueObjects;

namespace Communication.Application.Handlers.Commands;

/// <summary>
/// Handler para crear una nueva conversación.
/// </summary>
public sealed class CreateConversationHandler(
    IConversationRepository repository,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateConversationCommand, ConversationDto>
{
  private readonly IConversationRepository _repository = repository;
  private readonly IUnitOfWork _unitOfWork = unitOfWork;

  public async Task<Result<ConversationDto>> Handle(CreateConversationCommand request, CancellationToken cancellationToken)
  {
    var type = ConversationType.FromName<ConversationType>(request.Type) ?? ConversationType.Direct;

    var conversationResult = Conversation.Create(request.TenantId, request.Name, type);

    if (conversationResult.IsFailure)
      return Result<ConversationDto>.Failure(conversationResult.Error!);

    await _repository.AddAsync(conversationResult.Value!, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    return Result<ConversationDto>.Success(ConversationDto.FromDomain(conversationResult.Value!));
  }
}

/// <summary>
/// Handler para eliminar (soft delete) una conversación.
/// </summary>
public sealed class DeleteConversationHandler(
    IConversationRepository repository,
    IUnitOfWork unitOfWork) : ICommandHandler<DeleteConversationCommand, bool>
{
  private readonly IConversationRepository _repository = repository;
  private readonly IUnitOfWork _unitOfWork = unitOfWork;

  public async Task<Result<bool>> Handle(DeleteConversationCommand request, CancellationToken cancellationToken)
  {
    var conversation = await _repository.GetByIdAsync(request.TenantId, request.ConversationId, false, cancellationToken);

    if (conversation is null)
      return Result<bool>.Failure("Conversación no encontrada");

    if (conversation.IsDeleted)
      return Result<bool>.Failure("La conversación ya ha sido eliminada");

    conversation.Delete(request.DeletedBy);

    await _repository.UpdateAsync(conversation, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    return Result<bool>.Success(true);
  }
}

/// <summary>
/// Handler para restaurar una conversación eliminada.
/// </summary>
public sealed class RestoreConversationHandler(
    IConversationRepository repository,
    IUnitOfWork unitOfWork) : ICommandHandler<RestoreConversationCommand, ConversationDto>
{
  private readonly IConversationRepository _repository = repository;
  private readonly IUnitOfWork _unitOfWork = unitOfWork;

  public async Task<Result<ConversationDto>> Handle(RestoreConversationCommand request, CancellationToken cancellationToken)
  {
    var conversation = await _repository.GetByIdAsync(
        request.TenantId,
        request.ConversationId,
        includedDeleted: true,
        cancellationToken);

    if (conversation is null)
      return Result<ConversationDto>.Failure("Conversación no encontrada");

    if (!conversation.IsDeleted)
      return Result<ConversationDto>.Failure("La conversación no está eliminada");

    conversation.Restore();

    await _repository.UpdateAsync(conversation, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    return Result<ConversationDto>.Success(ConversationDto.FromDomain(conversation));
  }
}

/// <summary>
/// Handler para enviar un mensaje.
/// </summary>
public sealed class SendMessageHandler(
    IMessageRepository messageRepository,
    IConversationRepository conversationRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<SendMessageCommand, MessageDto>
{
  private readonly IMessageRepository _messageRepository = messageRepository;
  private readonly IConversationRepository _conversationRepository = conversationRepository;
  private readonly IUnitOfWork _unitOfWork = unitOfWork;

  public async Task<Result<MessageDto>> Handle(SendMessageCommand request, CancellationToken cancellationToken)
  {
    var conversation = await _conversationRepository.GetByIdAsync(request.TenantId, request.ConversationId, false, cancellationToken);

    if (conversation is null)
      return Result<MessageDto>.Failure("Conversación no encontrada");

    if (conversation.IsDeleted)
      return Result<MessageDto>.Failure("No se pueden enviar mensajes a una conversación eliminada");

    var messageResult = Message.Create(request.TenantId, request.ConversationId, request.SenderId, request.Content);

    if (messageResult.IsFailure)
      return Result<MessageDto>.Failure(messageResult.Error!);

    conversation.AddMessage(messageResult.Value!);

    await _messageRepository.AddAsync(messageResult.Value!, cancellationToken);
    await _conversationRepository.UpdateAsync(conversation, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    return Result<MessageDto>.Success(MessageDto.FromDomain(messageResult.Value!));
  }
}

/// <summary>
/// Handler para editar un mensaje.
/// </summary>
public sealed class EditMessageHandler(
    IMessageRepository repository,
    IUnitOfWork unitOfWork) : ICommandHandler<EditMessageCommand, MessageDto>
{
  private readonly IMessageRepository _repository = repository;
  private readonly IUnitOfWork _unitOfWork = unitOfWork;

  public async Task<Result<MessageDto>> Handle(EditMessageCommand request, CancellationToken cancellationToken)
  {
    var message = await _repository.GetByIdAsync(request.TenantId, request.MessageId, false, cancellationToken);

    if (message is null)
      return Result<MessageDto>.Failure("Mensaje no encontrado");

    var editResult = message.Edit(request.NewContent, request.SenderId);

    if (editResult.IsFailure)
      return Result<MessageDto>.Failure(editResult.Error!);

    await _repository.UpdateAsync(message, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    return Result<MessageDto>.Success(MessageDto.FromDomain(editResult.Value!));
  }
}

/// <summary>
/// Handler para eliminar (soft delete) un mensaje.
/// </summary>
public sealed class DeleteMessageHandler(
    IMessageRepository repository,
    IUnitOfWork unitOfWork) : ICommandHandler<DeleteMessageCommand, bool>
{
  private readonly IMessageRepository _repository = repository;
  private readonly IUnitOfWork _unitOfWork = unitOfWork;

  public async Task<Result<bool>> Handle(DeleteMessageCommand request, CancellationToken cancellationToken)
  {
    var message = await _repository.GetByIdAsync(request.TenantId, request.MessageId, false, cancellationToken);

    if (message is null)
      return Result<bool>.Failure("Mensaje no encontrado");

    if (message.IsDeleted)
      return Result<bool>.Failure("El mensaje ya ha sido eliminado");

    message.Delete(request.DeletedBy);

    await _repository.UpdateAsync(message, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    return Result<bool>.Success(true);
  }
}

/// <summary>
/// Handler para restaurar un mensaje eliminado.
/// </summary>
public sealed class RestoreMessageHandler(
    IMessageRepository repository,
    IUnitOfWork unitOfWork) : ICommandHandler<RestoreMessageCommand, MessageDto>
{
  private readonly IMessageRepository _repository = repository;
  private readonly IUnitOfWork _unitOfWork = unitOfWork;

  public async Task<Result<MessageDto>> Handle(RestoreMessageCommand request, CancellationToken ct)
  {
    var message = await _repository.GetByIdAsync(
        request.TenantId,
        request.MessageId,
        includedDeleted: true,
        ct);

    if (message is null)
      return Result<MessageDto>.Failure("Mensaje no encontrado");

    if (!message.IsDeleted)
      return Result<MessageDto>.Failure("El mensaje no está eliminado");

    message.Restore();

    await _repository.UpdateAsync(message, ct);
    await _unitOfWork.SaveChangesAsync(ct);

    return Result<MessageDto>.Success(MessageDto.FromDomain(message));
  }
}