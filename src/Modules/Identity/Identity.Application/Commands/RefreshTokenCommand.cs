using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Application.DTOs;

namespace Identity.Application.Commands;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<LoginResult>;
