using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;
using Projects.Domain.Events;
using Projects.Domain.ValueObjects;

namespace Projects.Domain.Entities;

/// <summary>
/// Entidad de dominio Project.
/// </summary>
public sealed class Project : AggregateRoot
{
  public Guid TenantId { get; private set; }
  public Guid SpaceId { get; private set; }
  public Guid? FolderId { get; private set; }
  public ProjectName Name { get; private set; } = null!;
  public string Description { get; private set; } = string.Empty;
  public DateOnly StartDate { get; private set; }
  public DateOnly EstimatedEndDate { get; private set; }
  public ProjectStatus Status { get; private set; } = null!;
  public Guid OwnerId { get; private set; }
  public List<Guid> TagIds { get; private set; } = new();

  // Soft Delete
  public bool IsDeleted { get; private set; }

  public DateTime? DeletedAt { get; private set; }
  public Guid? DeletedBy { get; private set; }

  private Project()
  { }

  public static Project Create(
      Guid tenantId,
      Guid spaceId,
      Guid? folderId,
      string name,
      string description,
      DateOnly estimatedEndDate,
      Guid ownerId)
  {
    var nameResult = ProjectName.Create(name);
    if (nameResult.IsFailure)
      throw new InvalidOperationException(nameResult.Error!);

    var project = new Project
    {
      Id = Guid.NewGuid(),
      TenantId = tenantId,
      SpaceId = spaceId,
      FolderId = folderId,
      Name = nameResult.Value!,
      Description = description,
      StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
      EstimatedEndDate = estimatedEndDate,
      Status = ProjectStatus.Planned,
      OwnerId = ownerId,
      IsDeleted = false,
      DeletedAt = null,
      DeletedBy = null
    };

    project.RaiseDomainEvent(new ProjectCreatedEvent(project.Id, tenantId, name));

    return project;
  }

  public Result<Project> Update(string? name, string? description, DateOnly? estimatedEndDate)
  {
    if (IsDeleted)
      return Result<Project>.Failure("No se puede modificar un proyecto eliminado");

    if (name is not null)
    {
      var nameResult = ProjectName.Create(name);
      if (nameResult.IsFailure)
        return Result<Project>.Failure(nameResult.Error!);
      Name = nameResult.Value!;
    }

    if (description is not null) Description = description;
    if (estimatedEndDate.HasValue) EstimatedEndDate = estimatedEndDate.Value;

    RaiseDomainEvent(new ProjectUpdatedEvent(Id, TenantId, Name.Value, Status.Value.ToString()));

    return Result<Project>.Success(this);
  }

  public Result ChangeStatus(ProjectStatus newStatus)
  {
    if (IsDeleted)
      return Result.Failure("No se puede cambiar el estado de un proyecto eliminado");

    if (Status == ProjectStatus.Done && newStatus != ProjectStatus.Done)
      return Result.Failure("No se puede reabrir un proyecto completado");

    Status = newStatus;
    RaiseDomainEvent(new ProjectStatusChangedEvent(Id, TenantId, newStatus.Name));

    return Result.Success();
  }

  /// <summary>
  /// Soft delete del proyecto.
  /// </summary>
  public void Delete(Guid deletedBy)
  {
    if (IsDeleted)
      throw new InvalidOperationException("El proyecto ya ha sido eliminado");

    IsDeleted = true;
    DeletedAt = DateTime.UtcNow;
    DeletedBy = deletedBy;

    RaiseDomainEvent(new ProjectDeletedEvent(Id, TenantId, deletedBy));
  }

  /// <summary>
  /// Restaura un proyecto eliminado.
  /// </summary>
  public void Restore()
  {
    if (!IsDeleted)
      throw new InvalidOperationException("El proyecto no está eliminado");

    IsDeleted = false;
    DeletedAt = null;
    DeletedBy = null;
  }

  public void AddTag(Guid tagId)
  {
    if (!TagIds.Contains(tagId))
    {
      TagIds.Add(tagId);
    }
  }

  public void RemoveTag(Guid tagId)
  {
    if (TagIds.Contains(tagId))
    {
      TagIds.Remove(tagId);
    }
  }
}