using BuildingBlocks.Domain;
using MediatR;

namespace Identity.Application.Queries;

public record UserPreferencesDto(string? SidebarPreferences);

public record GetUserPreferencesQuery(Guid UserId) : IRequest<Result<UserPreferencesDto>>;
