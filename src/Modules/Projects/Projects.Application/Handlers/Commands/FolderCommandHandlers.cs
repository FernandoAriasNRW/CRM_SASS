using BuildingBlocks.Application.Abstractions;
using Projects.Application.Abstractions;
using BuildingBlocks.Domain;
using Projects.Application.Abstractions.Repositories;
using Projects.Application.Commands;
using Projects.Domain.Entities;

namespace Projects.Application.Handlers.Commands;

public sealed class CreateFolderCommandHandler(IFolderRepository repository, IProjectsUnitOfWork unitOfWork) : ICommandHandler<CreateFolderCommand, Folder>
{
    public async Task<Result<Folder>> Handle(CreateFolderCommand request, CancellationToken cancellationToken)
    {
        var folder = Folder.Create(request.TenantId, request.SpaceId, request.Name);
        await repository.AddAsync(folder, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Folder>.Success(folder);
    }
}

public sealed class UpdateFolderCommandHandler(IFolderRepository repository, IProjectsUnitOfWork unitOfWork) : ICommandHandler<UpdateFolderCommand, bool>
{
    public async Task<Result<bool>> Handle(UpdateFolderCommand request, CancellationToken cancellationToken)
    {
        var folder = await repository.GetByIdAsync(request.TenantId, request.FolderId, false, cancellationToken);
        if (folder is null) return Result<bool>.Failure("Folder no encontrado");

        folder.Update(request.Name);
        await repository.UpdateAsync(folder, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public sealed class DeleteFolderCommandHandler(IFolderRepository repository, IProjectsUnitOfWork unitOfWork) : ICommandHandler<DeleteFolderCommand, bool>
{
    public async Task<Result<bool>> Handle(DeleteFolderCommand request, CancellationToken cancellationToken)
    {
        var folder = await repository.GetByIdAsync(request.TenantId, request.FolderId, false, cancellationToken);
        if (folder is null) return Result<bool>.Failure("Folder no encontrado");

        folder.Delete(request.DeletedBy);
        await repository.UpdateAsync(folder, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
