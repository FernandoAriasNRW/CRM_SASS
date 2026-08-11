using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Xunit;
using Identity.Application.DTOs;
using Identity.Infrastructure.Services;
using Identity.Application.Abstractions.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace UnitTests;

/// <summary>
/// Tests para JwtService - verificación de generación y configuración de tokens.
/// </summary>
public class JwtServiceTests
{
  private readonly IConfiguration _config;
  private readonly IJwtService _jwtService;
  private readonly Guid _testUserId = Guid.NewGuid();
  private readonly Guid _testTenantId = Guid.NewGuid();

  public JwtServiceTests()
  {
    _config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
          ["Jwt:Key"] = "test-key-for-unit-tests-min-32-characters!!",
          ["Jwt:Issuer"] = "test-issuer",
          ["Jwt:Audience"] = "test-audience"
        })
        .Build();

    _jwtService = new JwtService(_config);
  }

  #region GenerateTokens Tests

  [Fact]
  public void GenerateTokens_ReturnsValidAccessToken()
  {
    // Arrange
    var userDto = CreateTestUserDto();

    // Act
    var (accessToken, _, _, _) = _jwtService.GenerateTokens(userDto);

    // Assert
    accessToken.Should().NotBeNullOrEmpty();

    var handler = new JwtSecurityTokenHandler();
    var token = handler.ReadJwtToken(accessToken);

    token.Issuer.Should().Be("test-issuer");
    token.Audiences.Should().Contain("test-audience");
  }

  [Fact]
  public void GenerateTokens_ContainsCorrectClaims()
  {
    // Arrange
    var userDto = CreateTestUserDto();

    // Act
    var (accessToken, _, _, _) = _jwtService.GenerateTokens(userDto);

    // Assert
    var handler = new JwtSecurityTokenHandler();
    var token = handler.ReadJwtToken(accessToken);

    token.Subject.Should().Be(_testUserId.ToString());
    token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "test@example.com");
    token.Claims.Should().Contain(c => c.Type == "name" && c.Value == "Test User");
    token.Claims.Should().Contain(c => c.Type == "tenantId" && c.Value == _testTenantId.ToString());
    token.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");
  }

  [Fact]
  public void GenerateTokens_SetsCorrectExpiration()
  {
    // Arrange
    var userDto = CreateTestUserDto();
    var beforeGeneration = DateTime.UtcNow;

    // Act
    var (_, accessExpires, _, refreshExpires) = _jwtService.GenerateTokens(userDto);

    // Assert
    accessExpires.Should().BeAfter(beforeGeneration.AddMinutes(14));
    accessExpires.Should().BeBefore(beforeGeneration.AddMinutes(16)); // 15 min +- 1 min tolerance

    refreshExpires.Should().BeAfter(beforeGeneration.AddDays(6));
    refreshExpires.Should().BeBefore(beforeGeneration.AddDays(8)); // 7 days +- 1 day tolerance
  }

  [Fact]
  public void GenerateTokens_ReturnsUniqueRefreshTokens()
  {
    // Arrange
    var userDto = CreateTestUserDto();

    // Act
    var (_, _, refresh1, _) = _jwtService.GenerateTokens(userDto);
    var (_, _, refresh2, _) = _jwtService.GenerateTokens(userDto);

    // Assert
    refresh1.Should().NotBe(refresh2); // Tokens should be unique due to random generation
  }

  [Fact]
  public void GenerateTokens_ReturnsValidRefreshToken()
  {
    // Arrange
    var userDto = CreateTestUserDto();

    // Act
    var (_, _, refreshToken, _) = _jwtService.GenerateTokens(userDto);

    // Assert
    refreshToken.Should().NotBeNullOrEmpty();
    refreshToken.Length.Should().BeGreaterThan(50); // Base64 of 64 bytes
  }

  #endregion

  #region GenerateGuestToken Tests

  [Fact]
  public void GenerateGuestToken_ReturnsValidToken()
  {
    // Arrange
    var tenantId = Guid.NewGuid();
    var tenantSlug = "test-tenant";

    // Act
    var token = _jwtService.GenerateGuestToken(tenantId, tenantSlug);

    // Assert
    token.Should().NotBeNullOrEmpty();

    var handler = new JwtSecurityTokenHandler();
    var jwt = handler.ReadJwtToken(token);

    jwt.Claims.Should().Contain(c => c.Type == "tenantId" && c.Value == tenantId.ToString());
    jwt.Claims.Should().Contain(c => c.Type == "tenantSlug" && c.Value == tenantSlug);
    jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Guest");
    jwt.Claims.Should().Contain(c => c.Type == "scope" && c.Value == "tickets:create");
  }

  [Fact]
  public void GenerateGuestToken_HasCorrectExpiration()
  {
    // Arrange
    var tenantId = Guid.NewGuid();
    var beforeGeneration = DateTime.UtcNow;

    // Act
    var token = _jwtService.GenerateGuestToken(tenantId, "test");

    // Assert
    var handler = new JwtSecurityTokenHandler();
    var jwt = handler.ReadJwtToken(token);

    jwt.ValidTo.Should().BeAfter(beforeGeneration.AddMinutes(14));
    jwt.ValidTo.Should().BeBefore(beforeGeneration.AddMinutes(16)); // 15 min
  }

  #endregion

  #region Configuration Tests

  [Fact]
  public void Constructor_WithShortKey_ThrowsException()
  {
    // Arrange
    var badConfig = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
          ["Jwt:Key"] = "short-key",
          ["Jwt:Issuer"] = "test",
          ["Jwt:Audience"] = "test"
        })
        .Build();

    // Act & Assert
    Assert.Throws<InvalidOperationException>(() => new JwtService(badConfig));
  }

  [Fact]
  public void Constructor_WithMissingKey_ThrowsException()
  {
    // Arrange
    var badConfig = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
          ["Jwt:Issuer"] = "test",
          ["Jwt:Audience"] = "test"
        })
        .Build();

    // Act & Assert
    Assert.Throws<InvalidOperationException>(() => new JwtService(badConfig));
  }

  #endregion

  #region Helper Methods

  private UserDto CreateTestUserDto()
  {
    return new UserDto(
        Id: _testUserId,
        TenantId: _testTenantId,
        Name: "Test User",
        Email: "test@example.com",
        Role: "Admin",
        PasswordHash: null,
        IsActive: true,
        CreatedAt: DateTime.UtcNow
    );
  }

  #endregion
}
