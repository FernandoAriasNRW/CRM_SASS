using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Teams.Application.Abstractions.Repositories;
using Teams.Application.Commands;
using Teams.Domain.Entities;
using Teams.Domain.ValueObjects;

namespace Teams.Application.Handlers.Commands;

public sealed class CreateTeamCommandHandler(
    ITeamRepository repository,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateTeamCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        var team = Team.Create(request.TenantId, request.Name, request.Description);
        
        foreach (var memberId in request.MemberIds)
        {
            team.AddMember(memberId, TeamRole.Member);
        }

        await repository.AddAsync(team, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(team.Id);
    }
}

public sealed class UpdateTeamCommandHandler(
    ITeamRepository repository,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateTeamCommand, bool>
{
    public async Task<Result<bool>> Handle(UpdateTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await repository.GetByIdAsync(request.TenantId, request.TeamId, cancellationToken);
        if (team is null) return Result<bool>.Failure("Team not found");

        team.Update(request.Name, request.Description);

        await repository.UpdateAsync(team, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public sealed class DeleteTeamCommandHandler(
    ITeamRepository repository,
    IUnitOfWork unitOfWork) : ICommandHandler<DeleteTeamCommand, bool>
{
    public async Task<Result<bool>> Handle(DeleteTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await repository.GetByIdAsync(request.TenantId, request.TeamId, cancellationToken);
        if (team is null) return Result<bool>.Failure("Team not found");

        team.Delete();
        await repository.UpdateAsync(team, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
