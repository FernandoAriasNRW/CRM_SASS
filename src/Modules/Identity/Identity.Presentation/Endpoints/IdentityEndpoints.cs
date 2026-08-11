using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Identity.Application.Commands;
using Identity.Application.Queries;
using Identity.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Presentation.Endpoints;

public static class IdentityEndpoints
{
  private const string RefreshTokenCookieName = "crm_refresh_token";

  public static IServiceCollection AddIdentityPresentation(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddIdentityInfrastructure(configuration);
    return services;
  }

  public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
  {
    var authGroup = app.MapGroup("/api/v1/auth").WithTags("Auth");

    authGroup.MapPost("/login", async (LoginCommand command, IMediator mediator, HttpContext context) =>
    {
      var result = await mediator.Send(command);

      if (!result.IsSuccess)
        return Results.Unauthorized();

      var cookieOptions = new CookieOptions
      {
        HttpOnly = true,
        Secure = context.Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Expires = result.Value!.RefreshTokenExpiresAtUtc,
        Path = "/api/v1/auth"
      };
      context.Response.Cookies.Append(RefreshTokenCookieName, result.Value.RefreshToken, cookieOptions);

      return Results.Ok(new
      {
        accessToken = result.Value.AccessToken,
        accessTokenExpiresAtUtc = result.Value.AccessTokenExpiresAtUtc
      });
    });

    authGroup.MapPost("/refresh", async (HttpContext context, IMediator mediator) =>
    {
      if (!context.Request.Cookies.TryGetValue(RefreshTokenCookieName, out var refreshToken) || string.IsNullOrEmpty(refreshToken))
      {
        return Results.Unauthorized();
      }

      var result = await mediator.Send(new RefreshTokenCommand(refreshToken));
      
      if (!result.IsSuccess)
      {
        context.Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions { Path = "/api/v1/auth" });
        return Results.Unauthorized();
      }

      var cookieOptions = new CookieOptions
      {
        HttpOnly = true,
        Secure = context.Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Expires = result.Value!.RefreshTokenExpiresAtUtc,
        Path = "/api/v1/auth"
      };
      context.Response.Cookies.Append(RefreshTokenCookieName, result.Value.RefreshToken, cookieOptions);

      return Results.Ok(new
      {
        accessToken = result.Value.AccessToken,
        accessTokenExpiresAtUtc = result.Value.AccessTokenExpiresAtUtc
      });
    });

    authGroup.MapPost("/logout", async (HttpContext context, IMediator mediator) =>
    {
      if (context.Request.Cookies.TryGetValue(RefreshTokenCookieName, out var refreshToken) && !string.IsNullOrEmpty(refreshToken))
      {
        await mediator.Send(new LogoutCommand(refreshToken));
      }
      context.Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions { Path = "/api/v1/auth" });
      return Results.Ok();
    });

    authGroup.MapPost("/guest-token", async (GuestTokenCommand command, IMediator mediator) =>
    {
      var result = await mediator.Send(command);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
    }).RequireRateLimiting("guest-token");

    authGroup.MapGet("/users/me", async (IMediator mediator, ClaimsPrincipal principal) =>
    {
      var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
      if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var userId))
          return Results.Unauthorized();

      var result = await mediator.Send(new GetUserByIdQuery(userId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound();
    }).RequireAuthorization();

    var usersGroup = app.MapGroup("/api/v1/users").WithTags("Users");

    usersGroup.MapGet("/me/preferences", async (IMediator mediator, ClaimsPrincipal principal) =>
    {
      var userId = Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : Guid.Empty;
      if (userId == Guid.Empty) return Results.Unauthorized();
      
      var result = await mediator.Send(new GetUserPreferencesQuery(userId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
    }).RequireAuthorization();

    usersGroup.MapPut("/me/preferences", async (UpdateSidebarPreferencesCommand command, IMediator mediator, ClaimsPrincipal principal) =>
    {
      var userId = Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : Guid.Empty;
      if (userId == Guid.Empty) return Results.Unauthorized();
      
      var actualCommand = command with { UserId = userId };
      var result = await mediator.Send(actualCommand);
      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
    }).RequireAuthorization();

    usersGroup.MapGet("/tenant", async (IMediator mediator, ClaimsPrincipal principal) =>
    {
      var tenantId = Guid.TryParse(principal.FindFirstValue("tenantId"), out var tid) ? tid : Guid.Empty;
      if (tenantId == Guid.Empty) return Results.BadRequest("Invalid tenant");
      
      var result = await mediator.Send(new GetTenantUsersQuery(tenantId));
      return Results.Ok(result.Value);
    }).RequireAuthorization();

    usersGroup.MapPost("/me/avatar", async (Microsoft.AspNetCore.Http.IFormFile file, IMediator mediator, ClaimsPrincipal principal) =>
    {
      var userId = Guid.TryParse(principal.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier), out var uid) ? uid : Guid.Empty;
      if (userId == Guid.Empty) return Results.Unauthorized();

      if (file == null || file.Length == 0) return Results.BadRequest("File is empty");

      using var stream = file.OpenReadStream();
      var command = new UploadAvatarCommand(userId, stream, file.FileName, file.ContentType);
      
      var result = await mediator.Send(command);
      return result.IsSuccess ? Results.Ok(new { AvatarUrl = result.Value }) : Results.BadRequest(result.Error);
    }).RequireAuthorization().DisableAntiforgery();

    usersGroup.MapPut("/me/profile", async (UpdateProfileCommand command, IMediator mediator, ClaimsPrincipal principal) =>
    {
      var userId = Guid.TryParse(principal.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier), out var uid) ? uid : Guid.Empty;
      if (userId == Guid.Empty) return Results.Unauthorized();
      
      var actualCommand = command with { UserId = userId };
      var result = await mediator.Send(actualCommand);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    }).RequireAuthorization();

    usersGroup.MapGet("", async (IMediator mediator, ClaimsPrincipal principal) =>
    {
      var tenantId = Guid.TryParse(principal.FindFirstValue("tenantId"), out var tid) ? tid : Guid.Empty;
      var result = await mediator.Send(new GetTenantUsersQuery(tenantId));
      return Results.Ok(result.Value);
    }).RequireAuthorization();

    usersGroup.MapPost("", async (CreateUserRequest req, IMediator mediator, ClaimsPrincipal principal) =>
    {
      var tenantId = Guid.TryParse(principal.FindFirstValue("tenantId"), out var tid) ? tid : Guid.Empty;
      var command = new CreateUserCommand(tenantId, req.Name, req.Email, req.Password, req.Role);
      var result = await mediator.Send(command);
      return result.IsSuccess
              ? Results.Created($"/api/v1/users/{result.Value!.Id}", result.Value)
              : Results.BadRequest(result.Error);
    }).RequireAuthorization();

    usersGroup.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest req, IMediator mediator, ClaimsPrincipal principal) =>
    {
      var tenantId = Guid.TryParse(principal.FindFirstValue("tenantId"), out var tid) ? tid : Guid.Empty;
      var command = new UpdateUserCommand(tenantId, id, req.Name, req.Email, req.Role);
      var result = await mediator.Send(command);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    }).RequireAuthorization();

    usersGroup.MapDelete("/{id:guid}", async (Guid id, IMediator mediator, ClaimsPrincipal principal) =>
    {
      var tenantId = Guid.TryParse(principal.FindFirstValue("tenantId"), out var tid) ? tid : Guid.Empty;
      var currentUserId = Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : Guid.Empty;
      var command = new DeleteUserCommand(tenantId, id, currentUserId);
      var result = await mediator.Send(command);
      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
    }).RequireAuthorization();

    // Permissions endpoints
    var permissionsGroup = app.MapGroup("/api/v1/permissions").WithTags("Permissions").RequireAuthorization();

    permissionsGroup.MapGet("", async (string? targetType, Guid? targetId, string? roleName, IMediator mediator, ClaimsPrincipal principal) =>
    {
      var tenantIdStr = principal.FindFirstValue("tenantId") ?? principal.FindFirstValue("TenantId");
      var tenantId = Guid.TryParse(tenantIdStr, out var tid) ? tid : Guid.Empty;
      var query = new GetGranularPermissionsQuery(tenantId, targetType, targetId, roleName);
      var result = await mediator.Send(query);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    permissionsGroup.MapPost("", async (SaveGranularPermissionsRequest req, IMediator mediator, ClaimsPrincipal principal) =>
    {
      var tenantIdStr = principal.FindFirstValue("tenantId") ?? principal.FindFirstValue("TenantId");
      var tenantId = Guid.TryParse(tenantIdStr, out var tid) ? tid : Guid.Empty;
      var command = new SaveGranularPermissionsCommand(tenantId, req.TargetType, req.UserId, req.TeamId, req.RoleName, req.Permissions);
      var result = await mediator.Send(command);
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    var viewsGroup = app.MapGroup("/api/v1/views").WithTags("Views").RequireAuthorization();

    viewsGroup.MapGet("/{moduleName}", async (string moduleName, IMediator mediator, ClaimsPrincipal principal) =>
    {
      var tenantId = Guid.TryParse(principal.FindFirstValue("tenantId"), out var tid) ? tid : Guid.Empty;
      var userId = Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : Guid.Empty;
      var result = await mediator.Send(new GetSavedViewsQuery(tenantId, userId, moduleName));
      return Results.Ok(result.Value);
    });

    viewsGroup.MapPost("", async (SaveViewCommand command, IMediator mediator, ClaimsPrincipal principal) =>
    {
      var tenantId = Guid.TryParse(principal.FindFirstValue("tenantId"), out var tid) ? tid : Guid.Empty;
      var userId = Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : Guid.Empty;
      var actualCommand = command with { TenantId = tenantId, UserId = userId };
      var result = await mediator.Send(actualCommand);
      return result.IsSuccess
              ? Results.Created($"/api/v1/views", result.Value)
              : Results.BadRequest(result.Error);
    });

    viewsGroup.MapDelete("/{id:guid}", async (Guid id, IMediator mediator, ClaimsPrincipal principal) =>
    {
      var tenantId = Guid.TryParse(principal.FindFirstValue("tenantId"), out var tid) ? tid : Guid.Empty;
      var userId = Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : Guid.Empty;
      var result = await mediator.Send(new DeleteSavedViewCommand(tenantId, userId, id));
      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
    });

    return app;
  }
}

public record CreateUserRequest(string Name, string Email, string Password, string Role);
public record UpdateUserRequest(string Name, string Email, string Role);
public record SaveGranularPermissionsRequest(string TargetType, Guid? UserId, Guid? TeamId, string? RoleName, List<GranularPermissionInputItem> Permissions);
