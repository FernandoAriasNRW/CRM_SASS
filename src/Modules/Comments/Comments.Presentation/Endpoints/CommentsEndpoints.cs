using BuildingBlocks.Application.Abstractions;
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
  public sealed record NewCommentRequest(string Text, Guid? ReplyToId);

  public sealed record EditCommentRequest(string Text);

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

    static IResult ToResult(bool success, string? error)
    {
      if (success) return Results.Ok();

      // Un permiso denegado no es un dato inválido, y ninguno de los dos es «no existe». Que la
      // pantalla pueda distinguirlos es lo que le permite decir por qué no se pudo.
      if (error == Comment.Rules.NotFound) return Results.NotFound(error);
      if (error == Comment.Rules.OnlyAuthorEdits || error == Comment.Rules.OnlyAuthorOrAdminDeletes)
        return Results.Forbid();

      return Results.BadRequest(error);
    }

    group.MapGet("/{entityType}/{entityId:guid}", async (IUserContext currentUser, string entityType, Guid entityId, IMediator mediator) =>
    {
      var result = await mediator.Send(new GetCommentsQuery(currentUser.TenantId, entityType, entityId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapPost("/{entityType}/{entityId:guid}", async (IUserContext currentUser, string entityType, Guid entityId, NewCommentRequest body, IMediator mediator) =>
    {
      // El autor sale del token y no del cuerpo. Si viniera de fuera, cualquiera podría firmar
      // un comentario con el nombre de otro.
      var result = await mediator.Send(new AddCommentCommand(
          currentUser.TenantId, entityType, entityId, currentUser.UserId, body.Text, body.ReplyToId));

      return result.IsSuccess
          ? Results.Created($"/api/v1/comments/{entityType}/{entityId}", result.Value)
          : Results.BadRequest(result.Error);
    });

    group.MapPut("/{id:guid}", async (IUserContext currentUser, Guid id, EditCommentRequest body, IMediator mediator) =>
    {
      var result = await mediator.Send(new EditCommentCommand(
          currentUser.TenantId, id, currentUser.UserId, body.Text));

      return ToResult(result.IsSuccess, result.Error);
    });

    group.MapDelete("/{id:guid}", async (IUserContext currentUser, Guid id, IMediator mediator) =>
    {
      var result = await mediator.Send(new RemoveCommentCommand(
          currentUser.TenantId, id, currentUser.UserId, currentUser.Role));

      return result.IsSuccess ? Results.NoContent() : ToResult(false, result.Error);
    });

    return app;
  }
}
