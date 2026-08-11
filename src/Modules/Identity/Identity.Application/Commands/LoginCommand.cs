using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Application.DTOs;

namespace Identity.Application.Commands;

public sealed record LoginCommand(string Email, string Password) : ICommand<LoginResult>;
