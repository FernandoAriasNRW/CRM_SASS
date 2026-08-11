using BuildingBlocks.Domain;
using Identity.Application.Abstractions.Repositories;
using Identity.Application.Queries;
using MediatR;

namespace Identity.Application.Handlers;

public class GetUserPreferencesQueryHandler : IRequestHandler<GetUserPreferencesQuery, Result<UserPreferencesDto>>
{
    private readonly IUserRepository _userRepository;

    public GetUserPreferencesQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result<UserPreferencesDto>> Handle(GetUserPreferencesQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, false, cancellationToken);
        if (user is null)
            return Result<UserPreferencesDto>.Failure("Usuario no encontrado");

        return Result<UserPreferencesDto>.Success(new UserPreferencesDto(user.SidebarPreferences));
    }
}
