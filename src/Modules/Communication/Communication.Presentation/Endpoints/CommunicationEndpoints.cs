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

    group.MapGet("", async (System.Security.Claims.ClaimsPrincipal principal, string? type, IMediator mediator, int page = 1, int pageSize = 25) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var query = new GetConversationsQuery(tenantId, type, new() { Page = page, PageSize = pageSize });
      var result = await mediator.Send(query);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var result = await mediator.Send(new GetConversationByIdQuery(tenantId, id));
      return result.Value is null ? Results.NotFound() : Results.Ok(result.Value);
    });

    group.MapGet("/{id:guid}/messages", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator, int page = 1, int pageSize = 25) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var query = new GetMessagesQuery(tenantId, id, new() { Page = page, PageSize = pageSize });
      var result = await mediator.Send(query);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapPost("", async (CreateConversationCommand command, IMediator mediator) =>
    {
      var result = await mediator.Send(command);
      return result.IsSuccess
              ? Results.Created($"/api/v1/channels/{result.Value!.Id}", result.Value)
              : Results.BadRequest(result.Error);
    });

    group.MapPost("/{id:guid}/messages", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, Guid senderId, string content, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var command = new SendMessageCommand(tenantId, id, senderId, content);
      var result = await mediator.Send(command);
      return result.IsSuccess
              ? Results.Created($"/api/v1/channels/{id}/messages/{result.Value!.Id}", result.Value)
              : Results.BadRequest(result.Error);
    });

    group.MapPatch("/messages/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, Guid senderId, string newContent, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var command = new EditMessageCommand(tenantId, id, senderId, newContent);
      var result = await mediator.Send(command);
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    group.MapDelete("/messages/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, Guid actorId, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var command = new DeleteMessageCommand(tenantId, id, actorId);
      var result = await mediator.Send(command);
      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
    });

    app.MapHub<Communication.Presentation.Hubs.ChatHub>("/hubs/chat");

    return app;
  }
}
