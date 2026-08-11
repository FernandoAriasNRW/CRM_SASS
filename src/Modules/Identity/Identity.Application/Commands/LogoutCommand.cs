using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;

namespace Identity.Application.Commands;

public sealed record LogoutCommand(string? RefreshToken) : ICommand<bool>;
