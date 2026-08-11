using BuildingBlocks.Application.Abstractions;
using Identity.Application.DTOs;

namespace Identity.Application.Commands;

public sealed record UpdateProfileCommand(
    Guid UserId,
    string? Name,
    string? Email,
    string? PhoneNumber,
    string? Bio) : ICommand<UserDto>;
