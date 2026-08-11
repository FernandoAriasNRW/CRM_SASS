using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;

namespace Identity.Application.Commands;

public sealed record ChangePasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword
) : ICommand<bool>;
