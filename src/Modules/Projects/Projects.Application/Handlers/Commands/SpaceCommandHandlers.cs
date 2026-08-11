using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Projects.Application.Abstractions.Repositories;
using Projects.Application.Commands;
using Projects.Domain.Entities;

namespace Projects.Application.Handlers.Commands;

public sealed class CreateSpaceCommandHandler(ISpaceRepository repository, IUnitOfWork unitOfWork) : ICommandHandler<CreateSpaceCommand, Space>
{
    public async Task<Result<Space>> Handle(CreateSpaceCommand request, CancellationToken cancellationToken)
    {
        var space = Space.Create(request.TenantId, request.Name, request.Description, request.Color);
        await repository.AddAsync(space, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Space>.Success(space);
    }
}

public sealed class UpdateSpaceCommandHandler(ISpaceRepository repository, IUnitOfWork unitOfWork) : ICommandHandler<UpdateSpaceCommand, bool>
{
    public async Task<Result<bool>> Handle(UpdateSpaceCommand request, CancellationToken cancellationToken)
    {
        var space = await repository.GetByIdAsync(request.TenantId, request.SpaceId, false, cancellationToken);
        if (space is null) return Result<bool>.Failure("Space no encontrado");

        space.Update(request.Name, request.Description, request.Color);
        await repository.UpdateAsync(space, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public sealed class DeleteSpaceCommandHandler(ISpaceRepository repository, IUnitOfWork unitOfWork) : ICommandHandler<DeleteSpaceCommand, bool>
{
    public async Task<Result<bool>> Handle(DeleteSpaceCommand request, CancellationToken cancellationToken)
    {
        var space = await repository.GetByIdAsync(request.TenantId, request.SpaceId, false, cancellationToken);
        if (space is null) return Result<bool>.Failure("Space no encontrado");

        space.Delete(request.DeletedBy);
        await repository.UpdateAsync(space, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
