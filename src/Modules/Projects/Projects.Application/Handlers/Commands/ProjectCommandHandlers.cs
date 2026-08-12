using BuildingBlocks.Application.Abstractions;
using Projects.Application.Abstractions;
using BuildingBlocks.Domain;
using Projects.Application.Abstractions.Repositories;
using Projects.Application.Commands;
using Projects.Domain.Entities;
using Projects.Domain.ValueObjects;

namespace Projects.Application.Handlers.Commands;

public sealed class CreateProjectCommandHandler(
    IProjectRepository repository,
    IProjectsUnitOfWork unitOfWork) : ICommandHandler<CreateProjectCommand, Project>
{
  public async Task<Result<Project>> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
  {
    var project = Project.Create(
        request.TenantId, request.SpaceId, request.FolderId, request.Name, request.Description,
        request.EstimatedEndDate, request.OwnerId);

    await repository.AddAsync(project, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return Result<Project>.Success(project);
  }
}

public sealed class PatchProjectCommandHandler(
    IProjectRepository repository,
    IProjectsUnitOfWork unitOfWork) : ICommandHandler<PatchProjectCommand, bool>
{
  public async Task<Result<bool>> Handle(PatchProjectCommand request, CancellationToken cancellationToken)
  {
    var project = await repository.GetByIdAsync(request.TenantId, request.Id, false, cancellationToken);
    if (project is null)
      return Result<bool>.Failure("Proyecto no encontrado");

    project.Update(request.Name, request.Description, request.EstimatedEndDate);

    if (!string.IsNullOrEmpty(request.Status))
      project.ChangeStatus(new ProjectStatus(request.Status, request.Status));

    await repository.UpdateAsync(project, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    return Result<bool>.Success(true);
  }
}

public sealed class DeleteProjectCommandHandler(
    IProjectRepository repository,
    IProjectsUnitOfWork unitOfWork) : ICommandHandler<DeleteProjectCommand, bool>
{
  public async Task<Result<bool>> Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
  {
    var project = await repository.GetByIdAsync(request.TenantId, request.Id, false, cancellationToken);
    if (project is null)
      return Result<bool>.Failure("Proyecto no encontrado");

    if (project.IsDeleted)
      return Result<bool>.Failure("El proyecto ya ha sido eliminado");

    project.Delete(request.DeletedBy);

    await repository.UpdateAsync(project, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    return Result<bool>.Success(true);
  }
}

public sealed class RestoreProjectCommandHandler(
    IProjectRepository repository,
    IProjectsUnitOfWork unitOfWork) : ICommandHandler<RestoreProjectCommand, Project>
{
  public async Task<Result<Project>> Handle(RestoreProjectCommand request, CancellationToken cancellationToken)
  {
    var project = await repository.GetByIdAsync(request.TenantId, request.Id, includeDeleted: true, cancellationToken);
    if (project is null)
      return Result<Project>.Failure("Proyecto no encontrado");

    if (!project.IsDeleted)
      return Result<Project>.Failure("El proyecto no está eliminado");

    project.Restore();

    await repository.UpdateAsync(project, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return Result<Project>.Success(project);
  }
}
