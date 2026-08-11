using FluentAssertions;
using Xunit;
using NSubstitute;
using Identity.Application.Commands;
using Identity.Application.Queries;
using Identity.Application.Handlers.Commands;
using Identity.Application.Handlers.Queries;
using Identity.Application.Abstractions.Repositories;
using Identity.Application.Abstractions.Services;
using Identity.Application.Abstractions.Queries;
using Identity.Domain.Entities;
using Identity.Domain.ValueObjects;
using BuildingBlocks.Domain;
using BuildingBlocks.Application.Abstractions;
using Identity.Application.DTOs;

namespace UnitTests;

public class IdentityTests
{
    private readonly IUserRepository _userRepositoryMock;
    private readonly IJwtService _jwtServiceMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly IUserQueries _userQueriesMock;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public IdentityTests()
    {
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _jwtServiceMock = Substitute.For<IJwtService>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _userQueriesMock = Substitute.For<IUserQueries>();
    }

    #region Login Tests

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokens()
    {
        // Arrange
        var handler = new LoginCommandHandler(_userRepositoryMock, _jwtServiceMock);
        var command = new LoginCommand("test@test.com", "password123");

        var passwordHash = PasswordHash.Create("password123");
        var user = User.Create(_tenantId, "Test User", Email.Create("test@test.com").Value!, passwordHash, UserRole.Admin).Value;
        
        _userRepositoryMock.FindByEmailAsync("test@test.com", Arg.Any<CancellationToken>()).Returns(user);

        var expires = DateTime.UtcNow.AddHours(1);
        _jwtServiceMock.GenerateTokens(Arg.Any<UserDto>()).Returns(("access", expires, "refresh", expires));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.AccessToken.Should().Be("access");
        result.Value.RefreshToken.Should().Be("refresh");
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsFailure()
    {
        // Arrange
        var handler = new LoginCommandHandler(_userRepositoryMock, _jwtServiceMock);
        var command = new LoginCommand("test@test.com", "wrongpassword");

        var passwordHash = PasswordHash.Create("password123");
        var user = User.Create(_tenantId, "Test User", Email.Create("test@test.com").Value!, passwordHash, UserRole.Admin).Value;
        
        _userRepositoryMock.FindByEmailAsync("test@test.com", Arg.Any<CancellationToken>()).Returns(user);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Credenciales inválidas");
    }

    #endregion

    #region Refresh Token Tests

    [Fact]
    public async Task RefreshToken_WithAccessToken_Fails()
    {
        // Un access token está firmado con la misma clave y pasa ValidateToken.
        // Sólo el claim token_type impide usarlo para renovar sesión de forma
        // indefinida, así que este caso queda cubierto explícitamente.
        var principal = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
            [
                new System.Security.Claims.Claim("sub", _userId.ToString()),
                new System.Security.Claims.Claim("token_type", "access")
            ]));

        _jwtServiceMock.ValidateToken("access-token").Returns(principal);

        var handler = new RefreshTokenCommandHandler(_jwtServiceMock, _userRepositoryMock);

        var result = await handler.Handle(new RefreshTokenCommand("access-token"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Token de actualización inválido o expirado");
        await _userRepositoryMock.DidNotReceive()
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshToken_WithInvalidToken_Fails()
    {
        // Arrange: ValidateToken devuelve null para un token que no se puede validar.
        _jwtServiceMock.ValidateToken("token-invalido").Returns((System.Security.Claims.ClaimsPrincipal?)null);

        var handler = new RefreshTokenCommandHandler(_jwtServiceMock, _userRepositoryMock);
        var command = new RefreshTokenCommand("token-invalido");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Token de actualización inválido o expirado");
    }

    [Fact]
    public async Task RefreshToken_WithValidTokenButUnknownUser_Fails()
    {
        // Arrange: el token es válido y trae un 'sub', pero el usuario ya no existe.
        // Es el caso de un usuario eliminado que conserva un refresh token vigente:
        // no debe poder renovar sesión.
        var principal = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
            [
                new System.Security.Claims.Claim("sub", _userId.ToString()),
                new System.Security.Claims.Claim("token_type", "refresh")
            ]));

        _jwtServiceMock.ValidateToken("token-valido").Returns(principal);
        _userRepositoryMock.GetByIdAsync(_userId, false, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var handler = new RefreshTokenCommandHandler(_jwtServiceMock, _userRepositoryMock);

        // Act
        var result = await handler.Handle(new RefreshTokenCommand("token-valido"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Usuario no encontrado");
    }

    #endregion

    #region Logout Tests

    [Fact]
    public async Task Logout_ReturnsSuccess()
    {
        // Arrange
        var handler = new LogoutCommandHandler();
        var command = new LogoutCommand("any-token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    #endregion

    #region GetCurrentUser Tests

    [Fact]
    public async Task GetCurrentUser_WithValidId_ReturnsUserDto()
    {
        // Arrange
        var handler = new GetCurrentUserQueryHandler(_userQueriesMock);
        var query = new GetCurrentUserQuery(_userId);

        var userDto = new UserDto(
            Id: _userId,
            TenantId: _tenantId,
            Name: "Test User",
            Email: "test@test.com",
            Role: "Member",
            PasswordHash: null,
            IsActive: true,
            CreatedAt: DateTime.UtcNow
        );
        _userQueriesMock.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(userDto);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(_userId);
    }

    #endregion

    #region GuestToken Tests

    [Fact]
    public async Task GuestToken_WithValidTenant_ReturnsToken()
    {
        // Arrange
        var handler = new GuestTokenCommandHandler(_jwtServiceMock);
        var command = new GuestTokenCommand("test-tenant");

        _jwtServiceMock.GenerateGuestToken(Guid.Empty, "test-tenant").Returns("guest-token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.AccessToken.Should().Be("guest-token");
    }

    #endregion
}
