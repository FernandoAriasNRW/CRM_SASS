using BuildingBlocks.Domain;
using Identity.Application.Abstractions;
using BuildingBlocks.Application.Abstractions;
using Identity.Application.Abstractions.Repositories;
using Identity.Application.Abstractions.Services;
using Identity.Application.Commands;
using Identity.Application.DTOs;
using Identity.Domain.Entities;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Handlers.Commands;

/// <summary>
/// Handler para login de usuario.
/// </summary>
public sealed class LoginCommandHandler(
    IUserRepository userRepository,
    IJwtService jwtService) : ICommandHandler<LoginCommand, LoginResult>
{
  private readonly IUserRepository _userRepository = userRepository;
  private readonly IJwtService _jwtService = jwtService;

  public async Task<Result<LoginResult>> Handle(LoginCommand request, CancellationToken cancellationToken)
  {
    var user = await _userRepository.FindByEmailAsync(request.Email, cancellationToken);

    if (user is null || user.PasswordHash is null)
      return Result<LoginResult>.Failure("Credenciales inválidas");

    if (!PasswordHash.Verify(request.Password, user.PasswordHash.Value))
      return Result<LoginResult>.Failure("Credenciales inválidas");

    var (accessToken, accessExpires, refreshToken, refreshExpires) = _jwtService.GenerateTokens(UserDto.FromEntity(user));

    return Result<LoginResult>.Success(new LoginResult(
        accessToken,
        accessExpires,
        refreshToken,
        refreshExpires));
  }
}
/// <summary>
/// Handler para refresh token.
/// </summary>
public sealed class RefreshTokenCommandHandler(IJwtService jwtService, IUserRepository userRepository)
    : ICommandHandler<RefreshTokenCommand, LoginResult>
{
  private readonly IJwtService _jwtService = jwtService;
  private readonly IUserRepository _userRepository = userRepository;

  public async Task<Result<LoginResult>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
  {
      var principal = _jwtService.ValidateToken(request.RefreshToken);
      if (principal is null)
          return Result<LoginResult>.Failure("Token de actualización inválido o expirado");

      // Simétrico a la comprobación del middleware: un access token no debe
      // servir para renovar sesión indefinidamente.
      var tokenType = System.Linq.Enumerable
          .FirstOrDefault(principal.Claims, c => c.Type == "token_type")?.Value;
      if (tokenType != "refresh")
          return Result<LoginResult>.Failure("Token de actualización inválido o expirado");

      var userIdStr = System.Linq.Enumerable.FirstOrDefault(principal.Claims, c => c.Type == "sub")?.Value;
      if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
          return Result<LoginResult>.Failure("Token inválido");

      // Sin filtrar por tenant: al renovar no hay sesión activa, sólo la cookie.
      var user = await _userRepository.FindForSessionRenewalAsync(userId, cancellationToken);
      if (user is null)
          return Result<LoginResult>.Failure("Usuario no encontrado");

      var (accessToken, accessExpires, newRefreshToken, refreshExpires) = _jwtService.GenerateTokens(UserDto.FromEntity(user));

      return Result<LoginResult>.Success(new LoginResult(
          accessToken,
          accessExpires,
          newRefreshToken,
          refreshExpires));
  }
}
public sealed class GuestTokenCommandHandler(IJwtService jwtService)
    : ICommandHandler<GuestTokenCommand, GuestTokenResult>
{
  public Task<Result<GuestTokenResult>> Handle(GuestTokenCommand request, CancellationToken cancellationToken)
  {
    var token = jwtService.GenerateGuestToken(Guid.Empty, request.TenantSlug);
    return Task.FromResult(Result<GuestTokenResult>.Success(
        new GuestTokenResult(token, DateTime.UtcNow.AddMinutes(15))));
  }
}

/// <summary>
/// Handler para crear un nuevo usuario.
/// </summary>
public sealed class CreateUserCommandHandler(
    IUserRepository userRepository,
    IIdentityUnitOfWork unitOfWork) : ICommandHandler<CreateUserCommand, UserDto>
{
  private readonly IUserRepository _userRepository = userRepository;
  private readonly IIdentityUnitOfWork _unitOfWork = unitOfWork;

  public async Task<Result<UserDto>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
  {
    if (await _userRepository.EmailExistsAsync(request.Email, null, cancellationToken))
      return Result<UserDto>.Failure("El email ya está registrado");

    var emailResult = Email.Create(request.Email);
    if (emailResult.IsFailure)
      return Result<UserDto>.Failure(emailResult.Error!);

    PasswordHash passwordHash;
    try { passwordHash = PasswordHash.Create(request.Password); }
    catch (ArgumentException ex) { return Result<UserDto>.Failure(ex.Message); }

    var role = UserRole.FromName<UserRole>(request.Role) ?? UserRole.Member;
    var userResult = User.Create(request.TenantId, request.Name, emailResult.Value!, passwordHash, role);

    if (userResult.IsFailure)
      return Result<UserDto>.Failure(userResult.Error!);

    var user = userResult.Value!;
    await _userRepository.AddAsync(user, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    return Result<UserDto>.Success(UserDto.FromEntity(user));
  }
}

/// <summary>
/// Handler para actualizar un usuario.
/// </summary>
public sealed class UpdateUserCommandHandler(
    IUserRepository userRepository,
    IIdentityUnitOfWork unitOfWork) : ICommandHandler<UpdateUserCommand, UserDto>
{
  private readonly IUserRepository _userRepository = userRepository;
  private readonly IIdentityUnitOfWork _unitOfWork = unitOfWork;

  public async Task<Result<UserDto>> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
  {
    var user = await _userRepository.GetByIdAsync(request.UserId, includeDeleted: false, cancellationToken);

    if (user is null)
      return Result<UserDto>.Failure("Usuario no encontrado");

    if (user.IsDeleted)
      return Result<UserDto>.Failure("No se puede modificar un usuario eliminado");

    if (!string.IsNullOrEmpty(request.Email) &&
        await _userRepository.EmailExistsAsync(request.Email, request.UserId, cancellationToken))
      return Result<UserDto>.Failure("El email ya está en uso");

    Email? newEmail = null;
    if (!string.IsNullOrEmpty(request.Email))
    {
      var emailResult = Email.Create(request.Email);
      if (emailResult.IsFailure)
        return Result<UserDto>.Failure(emailResult.Error!);
      newEmail = emailResult.Value;
    }

    user.UpdateProfile(request.Name, newEmail, null, null);

    if (!string.IsNullOrEmpty(request.Role))
    {
      var newRole = UserRole.FromName<UserRole>(request.Role) ?? UserRole.Member;
      var roleResult = user.ChangeRole(newRole);
      if (roleResult.IsFailure)
        return Result<UserDto>.Failure(roleResult.Error!);
    }

    await _userRepository.UpdateAsync(user, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    return Result<UserDto>.Success(UserDto.FromEntity(user));
  }
}

/// <summary>
/// Handler para actualizar el perfil del usuario autenticado.
/// </summary>
public sealed class UpdateProfileCommandHandler(
    IUserRepository userRepository,
    IIdentityUnitOfWork unitOfWork) : ICommandHandler<UpdateProfileCommand, UserDto>
{
  private readonly IUserRepository _userRepository = userRepository;
  private readonly IIdentityUnitOfWork _unitOfWork = unitOfWork;

  public async Task<Result<UserDto>> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
  {
    var user = await _userRepository.GetByIdAsync(request.UserId, includeDeleted: false, cancellationToken);

    if (user is null)
      return Result<UserDto>.Failure("Usuario no encontrado");

    if (user.IsDeleted)
      return Result<UserDto>.Failure("No se puede modificar un usuario eliminado");

    if (!string.IsNullOrEmpty(request.Email) &&
        await _userRepository.EmailExistsAsync(request.Email, request.UserId, cancellationToken))
      return Result<UserDto>.Failure("El email ya está en uso");

    Email? newEmail = null;
    if (!string.IsNullOrEmpty(request.Email))
    {
      var emailResult = Email.Create(request.Email);
      if (emailResult.IsFailure)
        return Result<UserDto>.Failure(emailResult.Error!);
      newEmail = emailResult.Value;
    }

    user.UpdateProfile(request.Name, newEmail, request.PhoneNumber, request.Bio);

    await _userRepository.UpdateAsync(user, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    return Result<UserDto>.Success(UserDto.FromEntity(user));
  }
}

/// <summary>
/// Handler para eliminar (soft delete) un usuario.
/// </summary>
public sealed class DeleteUserCommandHandler(
    IUserRepository userRepository,
    IIdentityUnitOfWork unitOfWork) : ICommandHandler<DeleteUserCommand, bool>
{
  private readonly IUserRepository _userRepository = userRepository;
  private readonly IIdentityUnitOfWork _unitOfWork = unitOfWork;

  public async Task<Result<bool>> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
  {
    var user = await _userRepository.GetByIdAsync(request.UserId, includeDeleted: false, cancellationToken);

    if (user is null)
      return Result<bool>.Failure("Usuario no encontrado");

    if (user.IsDeleted)
      return Result<bool>.Failure("El usuario ya ha sido eliminado");

    // Verificar si es el último administrador (lógica de negocio)
    if (user.Role == UserRole.Admin)
    {
      var adminCount = await _userRepository.GetAdminCountAsync(cancellationToken);
      if (adminCount <= 1)
        return Result<bool>.Failure("No se puede eliminar al último administrador del sistema");
    }

    user.Delete(request.DeletedBy);

    await _userRepository.UpdateAsync(user, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    return Result<bool>.Success(true);
  }
}

/// <summary>
/// Handler para restaurar un usuario eliminado.
/// </summary>
public sealed class RestoreUserCommandHandler(
    IUserRepository userRepository,
    IIdentityUnitOfWork unitOfWork) : ICommandHandler<RestoreUserCommand, UserDto>
{
  private readonly IUserRepository _userRepository = userRepository;
  private readonly IIdentityUnitOfWork _unitOfWork = unitOfWork;

  public async Task<Result<UserDto>> Handle(RestoreUserCommand request, CancellationToken cancellationToken)
  {
    var user = await _userRepository.GetByIdAsync(request.UserId, includeDeleted: true, cancellationToken);

    if (user is null)
      return Result<UserDto>.Failure("Usuario no encontrado");

    if (!user.IsDeleted)
      return Result<UserDto>.Failure("El usuario no está eliminado");

    user.Restore();

    await _userRepository.UpdateAsync(user, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    return Result<UserDto>.Success(UserDto.FromEntity(user));
  }
}

/// <summary>
/// Handler para cambiar contraseña.
/// </summary>
public sealed class ChangePasswordCommandHandler(
    IUserRepository userRepository,
    IIdentityUnitOfWork unitOfWork) : ICommandHandler<ChangePasswordCommand, bool>
{
  private readonly IUserRepository _userRepository = userRepository;
  private readonly IIdentityUnitOfWork _unitOfWork = unitOfWork;

  public async Task<Result<bool>> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
  {
    var user = await _userRepository.GetByIdAsync(request.UserId, includeDeleted: false, cancellationToken);

    if (user is null)
      return Result<bool>.Failure("Usuario no encontrado");

    if (user.IsDeleted)
      return Result<bool>.Failure("No se puede cambiar la contraseña de un usuario eliminado");

    PasswordHash newHash;
    try { newHash = PasswordHash.Create(request.NewPassword); }
    catch (ArgumentException ex) { return Result<bool>.Failure(ex.Message); }

    user.ChangePassword(newHash);

    await _userRepository.UpdateAsync(user, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    return Result<bool>.Success(true);
  }
}

public sealed class LogoutCommandHandler : ICommandHandler<LogoutCommand, bool>
{
  public Task<Result<bool>> Handle(LogoutCommand request, CancellationToken cancellationToken)
  {
    return Task.FromResult(Result<bool>.Success(true));
  }
}
