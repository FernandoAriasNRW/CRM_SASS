using BuildingBlocks.Application.Abstractions;
using System.Linq;
using Communication.Application.Commands;
using Communication.Application.Queries;
using Communication.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Communication.Presentation.Endpoints;

public static class CommunicationEndpoints
{
  public static IServiceCollection AddCommunicationPresentation(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddCommunicationInfrastructure(configuration);
    return services;
  }

  public static IEndpointRouteBuilder MapCommunicationEndpoints(this IEndpointRouteBuilder app)
  {
    var group = app.MapGroup("/api/v1/channels").WithTags("Communication").RequireAuthorization();

    group.MapGet("", async (IUserContext currentUser, string? type, IMediator mediator, int page = 1, int pageSize = 25) =>
    {
      var tenantId = currentUser.TenantId;
      var query = new GetConversationsQuery(tenantId, type, new() { Page = page, PageSize = pageSize });
      var result = await mediator.Send(query);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/{id:guid}", async (IUserContext currentUser, Guid id, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var result = await mediator.Send(new GetConversationByIdQuery(tenantId, id));
      return result.Value is null ? Results.NotFound() : Results.Ok(result.Value);
    });

    group.MapGet("/{id:guid}/messages", async (IUserContext currentUser, Guid id, IMediator mediator, int page = 1, int pageSize = 25) =>
    {
      var tenantId = currentUser.TenantId;
      var query = new GetMessagesQuery(tenantId, id, new() { Page = page, PageSize = pageSize });
      var result = await mediator.Send(query);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    // Inquilino del token. Ver ProjectsEndpoints: misma grieta.
    group.MapPost("", async (CreateConversationCommand command, IUserContext usuario, IMediator mediator) =>
    {
      var result = await mediator.Send(command with { TenantId = usuario.TenantId });
      return result.IsSuccess
              ? Results.Created($"/api/v1/channels/{result.Value!.Id}", result.Value)
              : Results.BadRequest(result.Error);
    });

    // Quien firma el mensaje es quien lo manda, no quien lo diga la petición: `senderId` venía
    // en la cadena de consulta, así que cualquiera podía escribir en un canal con el nombre de
    // otra persona. Es la misma regla que en los comentarios, donde el autor sale del token.
    group.MapPost("/{id:guid}/messages", async (Guid id, string content, IUserContext usuario, IMediator mediator) =>
    {
      var command = new SendMessageCommand(usuario.TenantId, id, usuario.UserId, content);
      var result = await mediator.Send(command);
      return result.IsSuccess
              ? Results.Created($"/api/v1/channels/{id}/messages/{result.Value!.Id}", result.Value)
              : Results.BadRequest(result.Error);
    });

    // Quién edita sale del token. Venía por la URL, así que quien quisiera editar el mensaje de
    // otra persona sólo tenía que poner el identificador de esa persona en `senderId`.
    group.MapPatch("/messages/{id:guid}", async (Guid id, string newContent, IUserContext usuario, IMediator mediator) =>
    {
      var command = new EditMessageCommand(usuario.TenantId, id, usuario.UserId, newContent);
      var result = await mediator.Send(command);
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    // Igual al borrar: quien actúa es quien tiene el token, no quien diga la URL.
    group.MapDelete("/messages/{id:guid}", async (Guid id, IUserContext usuario, IMediator mediator) =>
    {
      var command = new DeleteMessageCommand(usuario.TenantId, id, usuario.UserId);
      var result = await mediator.Send(command);
      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
    });

    app.MapHub<Communication.Presentation.Hubs.ChatHub>("/hubs/chat");

    return app;
  }
}
