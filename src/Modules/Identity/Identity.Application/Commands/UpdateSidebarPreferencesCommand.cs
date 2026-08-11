using BuildingBlocks.Domain;
using MediatR;

namespace Identity.Application.Commands;

public record UpdateSidebarPreferencesCommand(Guid UserId, string PreferencesJson) : IRequest<Result>;
