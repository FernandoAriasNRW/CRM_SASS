using BuildingBlocks.Application.Abstractions;
using Communication.Application.DTOs;

namespace Communication.Application.Commands;

public sealed record RestoreMessageCommand(
    Guid TenantId,
    Guid MessageId,
    Guid RestoredBy
) : ICommand<MessageDto>;
