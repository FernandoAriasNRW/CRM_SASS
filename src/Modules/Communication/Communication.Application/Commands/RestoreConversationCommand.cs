using BuildingBlocks.Application.Abstractions;
using Communication.Application.DTOs;

namespace Communication.Application.Commands;



public sealed record RestoreConversationCommand(
    Guid TenantId,
    Guid ConversationId,
    Guid RestoredBy
) : ICommand<ConversationDto>;