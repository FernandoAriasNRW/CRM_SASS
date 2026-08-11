using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Application.DTOs;

namespace Identity.Application.Commands;

public sealed record GuestTokenCommand(string TenantSlug) : ICommand<GuestTokenResult>;
