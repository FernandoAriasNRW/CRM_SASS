using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;
using Identity.Domain.Events;
using Identity.Domain.ValueObjects;

namespace Identity.Domain.Entities;

/// <summary>
/// Entidad de dominio User.
/// </summary>
public sealed class User : AggregateRoot, ITenantEntity, ISoftDeletable
{
  public Guid TenantId { get; private set; }
  public string Name { get; private set; } = string.Empty;
  public Email Email { get; private set; } = null!;
  public PasswordHash PasswordHash { get; private set; } = null!;
  public UserRole Role { get; private set; } = null!;
  public string? AvatarUrl { get; private set; }
  public string? PhoneNumber { get; private set; }
  public string? Bio { get; private set; }
  public string? SidebarPreferences { get; private set; }
  public DateTime CreatedAtUtc { get; private set; }

  // Soft Delete
  public bool IsDeleted { get; private set; }

  public DateTime? DeletedAt { get; private set; }
  public Guid? DeletedBy { get; private set; }

  private User()
  { }

  public static Result<User> Create(
      Guid tenantId,
      string name,
      Email email,
      PasswordHash passwordHash,
      UserRole role = null!)
  {
    role ??= UserRole.Member;

    if (string.IsNullOrWhiteSpace(name))
      return Result<User>.Failure("El nombre es requerido");

    var user = new User
    {
      Id = Guid.NewGuid(),
      TenantId = tenantId,
      Name = name,
      Email = email,
      PasswordHash = passwordHash,
      Role = role,
      AvatarUrl = null,
      PhoneNumber = null,
      Bio = null,
      SidebarPreferences = null,
      CreatedAtUtc = DateTime.UtcNow,
      IsDeleted = false,
      DeletedAt = null,
      DeletedBy = null
    };

    user.RaiseDomainEvent(new UserCreatedEvent(user.Id, tenantId, email.Value));

    return Result<User>.Success(user);
  }

  public bool ValidatePassword(string plainPassword)
  {
    try
    {
      return PasswordHash.Verify(plainPassword, PasswordHash.Value);
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error validating password: {ex.Message}");
      return false;
    }
  }

  public void UpdateProfile(string? name, Email? email, string? phoneNumber, string? bio)
  {
    if (IsDeleted)
      throw new InvalidOperationException("No se puede modificar un usuario eliminado");

    if (!string.IsNullOrWhiteSpace(name))
      Name = name;

    if (email is not null)
      Email = email;

    if (phoneNumber is not null)
      PhoneNumber = phoneNumber;

    if (bio is not null)
      Bio = bio;

    RaiseDomainEvent(new UserUpdatedEvent(Id, TenantId));
  }

  public void UpdateAvatarUrl(string avatarUrl)
  {
    if (IsDeleted)
      throw new InvalidOperationException("No se puede modificar un usuario eliminado");

    AvatarUrl = avatarUrl;
    RaiseDomainEvent(new UserUpdatedEvent(Id, TenantId));
  }

  public void UpdateSidebarPreferences(string preferencesJson)
  {
    if (IsDeleted)
      throw new InvalidOperationException("No se puede modificar un usuario eliminado");

    SidebarPreferences = preferencesJson;
    RaiseDomainEvent(new UserUpdatedEvent(Id, TenantId));
  }

  public void ChangePassword(PasswordHash newPasswordHash)
  {
    if (IsDeleted)
      throw new InvalidOperationException("No se puede cambiar la contraseña de un usuario eliminado");

    PasswordHash = newPasswordHash;
    RaiseDomainEvent(new PasswordChangedEvent(Id));
  }

  public Result ChangeRole(UserRole newRole)
  {
    if (IsDeleted)
      return Result.Failure("No se puede cambiar el rol de un usuario eliminado");

    if (Role == UserRole.Admin && newRole != UserRole.Admin)
    {
      // Verificar que no sea el último admin Esta lógica debería verificarse en el Application layer
    }

    Role = newRole;
    return Result.Success();
  }

  /// <summary>
  /// Soft delete del usuario.
  /// </summary>
  public void Delete(Guid deletedBy)
  {
    if (IsDeleted)
      throw new InvalidOperationException("El usuario ya ha sido eliminado");

    // No permitir eliminar al último administrador
    if (Role == UserRole.Admin)
    {
      // Esta validación debería hacerse en el Application layer para verificar si es el último admin del sistema
    }

    IsDeleted = true;
    DeletedAt = DateTime.UtcNow;
    DeletedBy = deletedBy;

    RaiseDomainEvent(new UserDeletedEvent(Id, TenantId, deletedBy));
  }

  /// <summary>
  /// Restaura un usuario eliminado.
  /// </summary>
  public void Restore()
  {
    if (!IsDeleted)
      throw new InvalidOperationException("El usuario no está eliminado");

    IsDeleted = false;
    DeletedAt = null;
    DeletedBy = null;
  }

  /// <summary>
  /// Verifica si el usuario puede ser modificado.
  /// </summary>
  public bool CanBeModified() => !IsDeleted;

  /// <summary>
  /// Verifica si el usuario está activo.
  /// </summary>
  public bool IsActive() => !IsDeleted;
}