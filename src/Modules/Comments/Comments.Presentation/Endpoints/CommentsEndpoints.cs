using System.Security.Claims;
using Comments.Application;
using Comments.Domain.Entities;
using Comments.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Comments.Presentation.Endpoints;

public static class CommentsEndpoints
{
  /// <summary>El cuerpo que manda la interfaz: sólo el texto y, si acaso, a qué responde.</summary>
  public sealed record NuevoComentario(string Texto, Guid? RespondeAId);

  public sealed record TextoDelComentario(string Texto);

  public static IServiceCollection AddCommentsPresentation(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddCommentsInfrastructure(configuration);
    return services;
  }

  public static IEndpointRouteBuilder MapCommentsEndpoints(this IEndpointRouteBuilder app)
  {
    // Una sola familia de rutas para las tres entidades, en lugar de colgarlas de
    // `/tasks/{id}/comments`, `/tickets/{id}/comments` y `/projects/{id}/comments`. Comentar es
    // la misma operación en los tres sitios; tres familias serían tres sitios donde arreglar el
    // mismo fallo.
    var group = app.MapGroup("/api/v1/comments").WithTags("Comments").RequireAuthorization();

    static Guid TenantDe(ClaimsPrincipal principal)
        => Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var id) ? id : Guid.Empty;

    static Guid UsuarioDe(ClaimsPrincipal principal)
        => Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value, out var id) ? id : Guid.Empty;

    static string RolDe(ClaimsPrincipal principal)
        => principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? string.Empty;

    static IResult Responder(bool exito, string? error)
    {
      if (exito) return Results.Ok();

      // Un permiso denegado no es un dato inválido, y ninguno de los dos es «no existe». Que la
      // pantalla pueda distinguirlos es lo que le permite decir por qué no se pudo.
      if (error == Comment.Reglas.NoEncontrado) return Results.NotFound(error);
      if (error == Comment.Reglas.SoloElAutorEdita || error == Comment.Reglas.SoloElAutorOAdminBorra)
        return Results.Forbid();

      return Results.BadRequest(error);
    }

    group.MapGet("/{entidad}/{entityId:guid}", async (ClaimsPrincipal principal, string entidad, Guid entityId, IMediator mediator) =>
    {
      var result = await mediator.Send(new GetCommentsQuery(TenantDe(principal), entidad, entityId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapPost("/{entidad}/{entityId:guid}", async (ClaimsPrincipal principal, string entidad, Guid entityId, NuevoComentario cuerpo, IMediator mediator) =>
    {
      // El autor sale del token y no del cuerpo. Si viniera de fuera, cualquiera podría firmar
      // un comentario con el nombre de otro.
      var result = await mediator.Send(new AddCommentCommand(
          TenantDe(principal), entidad, entityId, UsuarioDe(principal), cuerpo.Texto, cuerpo.RespondeAId));

      return result.IsSuccess
          ? Results.Created($"/api/v1/comments/{entidad}/{entityId}", result.Value)
          : Results.BadRequest(result.Error);
    });

    group.MapPut("/{id:guid}", async (ClaimsPrincipal principal, Guid id, TextoDelComentario cuerpo, IMediator mediator) =>
    {
      var result = await mediator.Send(new EditCommentCommand(
          TenantDe(principal), id, UsuarioDe(principal), cuerpo.Texto));

      return Responder(result.IsSuccess, result.Error);
    });

    group.MapDelete("/{id:guid}", async (ClaimsPrincipal principal, Guid id, IMediator mediator) =>
    {
      var result = await mediator.Send(new RemoveCommentCommand(
          TenantDe(principal), id, UsuarioDe(principal), RolDe(principal)));

      return result.IsSuccess ? Results.NoContent() : Responder(false, result.Error);
    });

    return app;
  }
}
