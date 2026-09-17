using BuildingBlocks.Application.Abstractions;
using Identity.Application.Favoritos;
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

  /// <summary>
  /// Gestionar personas y permisos es cosa de administradores.
  ///
  /// <b>Hasta septiembre de 2026 bastaba con haber iniciado sesión.</b> La pantalla de
  /// administración tenía su guarda, pero la API no: cualquier miembro podía crear un usuario con
  /// rol «Admin» —comprobado contra la aplicación levantada, respondía 201—, cambiarse el rol a sí
  /// mismo o darse permisos. Esconder el botón no protege nada si el endpoint contesta.
  /// </summary>
  private static void SoloAdministradores(Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder politica)
    => politica.RequireRole("Admin");

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

    // Aquí había un POST /auth/guest-token, anónimo, que emitía un token con rol «Guest». Ese token
    // pasaba cualquier RequireAuthorization() de la API, no sólo el alta de tickets para la que se
    // pensó. Nunca llegó a funcionar —su política de límite de peticiones no existía y respondía
    // 409— y se quitó antes de que alguien la arreglara y abriera la API entera.

    authGroup.MapGet("/users/me", async (IMediator mediator, IUserContext currentUser) =>
    {
      if (currentUser.UserId == Guid.Empty)
          return Results.Unauthorized();

      var result = await mediator.Send(new GetUserByIdQuery(currentUser.UserId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound();
    }).RequireAuthorization();

    var usersGroup = app.MapGroup("/api/v1/users").WithTags("Users");

    // Favoritos. Van bajo el usuario y no bajo cada módulo porque una estrella no es un atributo
    // de la tarea: es algo que una persona decidió sobre ella. Ver Favorito para el porqué.
    usersGroup.MapGet("/me/favoritos/{tipo}", async (
        string tipo, IUserContext usuario, IMediator mediator) =>
    {
      var result = await mediator.Send(new GetFavoritosQuery(usuario.TenantId, usuario.UserId, tipo));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    }).RequireAuthorization();

    // Un solo endpoint que alterna, no uno para marcar y otro para desmarcar: la estrella es un
    // interruptor, y con dos endpoints dos pestañas abiertas acaban peleándose.
    usersGroup.MapPost("/me/favoritos/{tipo}/{entityId:guid}", async (
        string tipo, Guid entityId, IUserContext usuario, IMediator mediator) =>
    {
      var result = await mediator.Send(
          new AlternarFavoritoCommand(usuario.TenantId, usuario.UserId, tipo, entityId));

      return result.IsSuccess
          ? Results.Ok(new { marcado = result.Value })
          : Results.BadRequest(result.Error);
    }).RequireAuthorization();

    // Compartición. Va bajo el elemento y no bajo el usuario, al revés que los favoritos: un
    // favorito es una decisión sobre mí, y compartir es una decisión sobre la cosa.
    var comparticionGroup = app.MapGroup("/api/v1/comparticion").WithTags("Comparticion");

    comparticionGroup.MapGet("/{tipo}/{entityId:guid}", async (
        string tipo, Guid entityId, IUserContext usuario, IMediator mediator) =>
    {
      var result = await mediator.Send(
          new Identity.Application.Comparticion.GetCompartidoConQuery(usuario.TenantId, tipo, entityId));

      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    }).RequireAuthorization();

    comparticionGroup.MapPut("/{tipo}/{entityId:guid}/{conUsuarioId:guid}", async (
        string tipo, Guid entityId, Guid conUsuarioId, CompartirRequest cuerpo,
        IUserContext usuario, IMediator mediator) =>
    {
      // PUT y no POST: compartir con la misma persona dos veces deja el mismo estado, cambiando
      // el nivel si hace falta. Con POST, la segunda llamada tendría que decidir si es un
      // conflicto, y no lo es.
      var result = await mediator.Send(new Identity.Application.Comparticion.CompartirCommand(
          usuario.TenantId, tipo, entityId, conUsuarioId, cuerpo.Nivel));

      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
    }).RequireAuthorization();

    comparticionGroup.MapDelete("/{tipo}/{entityId:guid}/{conUsuarioId:guid}", async (
        string tipo, Guid entityId, Guid conUsuarioId, IUserContext usuario, IMediator mediator) =>
    {
      var result = await mediator.Send(new Identity.Application.Comparticion.DejarDeCompartirCommand(
          usuario.TenantId, tipo, entityId, conUsuarioId));

      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
    }).RequireAuthorization();

    usersGroup.MapGet("/me/preferences", async (IMediator mediator, IUserContext currentUser) =>
    {
      var userId = currentUser.UserId;
      if (userId == Guid.Empty) return Results.Unauthorized();
      
      var result = await mediator.Send(new GetUserPreferencesQuery(userId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
    }).RequireAuthorization();

    usersGroup.MapPut("/me/preferences", async (UpdateSidebarPreferencesCommand command, IMediator mediator, IUserContext currentUser) =>
    {
      var userId = currentUser.UserId;
      if (userId == Guid.Empty) return Results.Unauthorized();
      
      var actualCommand = command with { UserId = userId };
      var result = await mediator.Send(actualCommand);
      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
    }).RequireAuthorization();

    usersGroup.MapGet("/tenant", async (IMediator mediator, IUserContext currentUser) =>
    {
      var tenantId = currentUser.TenantId;
      if (tenantId == Guid.Empty) return Results.BadRequest("Invalid tenant");
      
      var result = await mediator.Send(new GetTenantUsersQuery(tenantId));
      return Results.Ok(result.Value);
    }).RequireAuthorization();

    usersGroup.MapPost("/me/avatar", async (Microsoft.AspNetCore.Http.IFormFile file, IMediator mediator, IUserContext currentUser) =>
    {
      var userId = currentUser.UserId;
      if (userId == Guid.Empty) return Results.Unauthorized();

      if (file == null || file.Length == 0) return Results.BadRequest("File is empty");

      using var stream = file.OpenReadStream();
      var command = new UploadAvatarCommand(userId, stream, file.FileName, file.ContentType);
      
      var result = await mediator.Send(command);
      return result.IsSuccess ? Results.Ok(new { AvatarUrl = result.Value }) : Results.BadRequest(result.Error);
    }).RequireAuthorization().DisableAntiforgery();

    usersGroup.MapPut("/me/profile", async (UpdateProfileCommand command, IMediator mediator, IUserContext currentUser) =>
    {
      var userId = currentUser.UserId;
      if (userId == Guid.Empty) return Results.Unauthorized();
      
      var actualCommand = command with { UserId = userId };
      var result = await mediator.Send(actualCommand);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    }).RequireAuthorization();

    /// <summary>
    /// Cambiar la propia contraseña.
    ///
    /// <b>El comando, su handler y su validador existían desde el principio y ningún endpoint los
    /// exponía.</b> La pantalla de perfil llamaba a `/profile/password`, que no es ninguna ruta:
    /// cambiar la contraseña devolvía 404 y el aviso decía «El recurso solicitado no existe».
    ///
    /// El identificador sale del token y no del cuerpo: si viniera de fuera, cualquiera podría
    /// mandar el de otra persona y cambiarle la contraseña conociendo sólo la suya.
    /// </summary>
    usersGroup.MapPut("/me/password", async (CambiarContrasenaRequest req, IMediator mediator, IUserContext currentUser) =>
    {
      var userId = currentUser.UserId;
      if (userId == Guid.Empty) return Results.Unauthorized();

      var result = await mediator.Send(new ChangePasswordCommand(userId, req.CurrentPassword, req.NewPassword));
      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
    }).RequireAuthorization();

    usersGroup.MapGet("", async (string? search, int? pageSize, IMediator mediator, IUserContext currentUser) =>
    {
      var tenantId = currentUser.TenantId;

      // `search` con el mismo nombre que en tareas, tickets y proyectos. Cuatro endpoints con
      // cuatro nombres para lo mismo es cómo el frontend acaba llamando `q` en un sitio y `search`
      // en otro, y alguien probando a ver cuál funciona.
      var result = await mediator.Send(new GetTenantUsersQuery(tenantId, search, pageSize));
      return Results.Ok(result.Value);
    }).RequireAuthorization();

    usersGroup.MapPost("", async (CreateUserRequest req, IMediator mediator, IUserContext currentUser) =>
    {
      var tenantId = currentUser.TenantId;
      var command = new CreateUserCommand(tenantId, req.Name, req.Email, req.Password, req.Role);
      var result = await mediator.Send(command);
      return result.IsSuccess
              ? Results.Created($"/api/v1/users/{result.Value!.Id}", result.Value)
              : Results.BadRequest(result.Error);
    }).RequireAuthorization(SoloAdministradores);

    usersGroup.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest req, IMediator mediator, IUserContext currentUser) =>
    {
      var tenantId = currentUser.TenantId;
      var command = new UpdateUserCommand(tenantId, id, req.Name, req.Email, req.Role);
      var result = await mediator.Send(command);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    }).RequireAuthorization(SoloAdministradores);

    usersGroup.MapDelete("/{id:guid}", async (Guid id, IMediator mediator, IUserContext currentUser) =>
    {
      var tenantId = currentUser.TenantId;
      var currentUserId = currentUser.UserId;
      var command = new DeleteUserCommand(tenantId, id, currentUserId);
      var result = await mediator.Send(command);
      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
    }).RequireAuthorization(SoloAdministradores);

    // Permissions endpoints
    var permissionsGroup = app.MapGroup("/api/v1/permissions").WithTags("Permissions").RequireAuthorization(SoloAdministradores);

    permissionsGroup.MapGet("", async (string? targetType, Guid? targetId, string? roleName, IMediator mediator, IUserContext currentUser) =>
    {
      var tenantId = currentUser.TenantId;
      var query = new GetGranularPermissionsQuery(tenantId, targetType, targetId, roleName);
      var result = await mediator.Send(query);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    permissionsGroup.MapPost("", async (SaveGranularPermissionsRequest req, IMediator mediator, IUserContext currentUser) =>
    {
      var tenantId = currentUser.TenantId;
      var command = new SaveGranularPermissionsCommand(tenantId, req.TargetType, req.UserId, req.TeamId, req.RoleName, req.Permissions);
      var result = await mediator.Send(command);
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    var viewsGroup = app.MapGroup("/api/v1/views").WithTags("Views").RequireAuthorization();

    viewsGroup.MapGet("/{moduleName}", async (string moduleName, IMediator mediator, IUserContext currentUser) =>
    {
      var tenantId = currentUser.TenantId;
      var userId = currentUser.UserId;
      var result = await mediator.Send(new GetSavedViewsQuery(tenantId, userId, moduleName));
      return Results.Ok(result.Value);
    });

    viewsGroup.MapPost("", async (SaveViewCommand command, IMediator mediator, IUserContext currentUser) =>
    {
      var tenantId = currentUser.TenantId;
      var userId = currentUser.UserId;
      var actualCommand = command with { TenantId = tenantId, UserId = userId };
      var result = await mediator.Send(actualCommand);
      return result.IsSuccess
              ? Results.Created($"/api/v1/views", result.Value)
              : Results.BadRequest(result.Error);
    });

    viewsGroup.MapDelete("/{id:guid}", async (Guid id, IMediator mediator, IUserContext currentUser) =>
    {
      var tenantId = currentUser.TenantId;
      var userId = currentUser.UserId;
      var result = await mediator.Send(new DeleteSavedViewCommand(tenantId, userId, id));
      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
    });

    return app;
  }
}

public record CreateUserRequest(string Name, string Email, string Password, string Role);

/// <summary>Lo que hace falta para cambiar la propia contraseña. El usuario sale del token.</summary>
public record CambiarContrasenaRequest(string CurrentPassword, string NewPassword);
public record UpdateUserRequest(string Name, string Email, string Role);
public record SaveGranularPermissionsRequest(string TargetType, Guid? UserId, Guid? TeamId, string? RoleName, List<GranularPermissionInputItem> Permissions);

/// <summary>Con qué nivel se comparte: «View», «Edit» o «Full».</summary>
public sealed record CompartirRequest(string Nivel);
