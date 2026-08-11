using BuildingBlocks.Domain;
using Identity.Application.Abstractions.Repositories;
using Identity.Application.Commands;
using MediatR;

namespace Identity.Application.Handlers;

public class UpdateSidebarPreferencesCommandHandler : IRequestHandler<UpdateSidebarPreferencesCommand, Result>
{
    private readonly IUserRepository _userRepository;

    public UpdateSidebarPreferencesCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result> Handle(UpdateSidebarPreferencesCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, false, cancellationToken);
        if (user is null)
            return Result.Failure("Usuario no encontrado");

        user.UpdateSidebarPreferences(request.PreferencesJson);
        await _userRepository.UpdateAsync(user);

        return Result.Success();
    }
}
