using System.Linq;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Projects.Application.Commands;
using Projects.Application.Queries;
using Projects.Infrastructure;

namespace Projects.Presentation.Endpoints;

public static class ProjectsEndpoints
{
  public static IServiceCollection AddProjectsPresentation(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddProjectsInfrastructure(configuration);
    return services;
  }

  public static IEndpointRouteBuilder MapProjectsEndpoints(this IEndpointRouteBuilder app)
  {
    var group = app.MapGroup("/api/v1/projects").WithTags("Projects").RequireAuthorization();

    group.MapGet("", async (System.Security.Claims.ClaimsPrincipal principal, [Microsoft.AspNetCore.Mvc.FromQuery] string? status, [Microsoft.AspNetCore.Mvc.FromQuery] Guid? ownerId, [Microsoft.AspNetCore.Mvc.FromQuery] Guid? spaceId, [Microsoft.AspNetCore.Mvc.FromQuery] Guid? folderId, [Microsoft.AspNetCore.Mvc.FromQuery] string? filter, IMediator mediator, [Microsoft.AspNetCore.Mvc.FromQuery] int page = 1, [Microsoft.AspNetCore.Mvc.FromQuery] int pageSize = 25) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var userId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;
      var result = await mediator.Send(new GetProjectsQuery(tenantId, status, ownerId, spaceId, folderId, filter, userId, new() { Page = page, PageSize = pageSize }));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var result = await mediator.Send(new GetProjectByIdQuery(tenantId, id));
      return result.Value is null ? Results.NotFound() : Results.Ok(result.Value);
    });

    group.MapPost("", async (CreateProjectCommand command, IMediator mediator) =>
    {
      var result = await mediator.Send(command);
      return result.IsSuccess
              ? Results.Created($"/api/v1/projects/{result.Value!.Id}", result.Value)
              : Results.BadRequest(result.Error);
    });

    group.MapPatch("/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, PatchProjectCommand command, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var actualCommand = new PatchProjectCommand(tenantId, id, command.Name, command.Description, command.Status, command.EstimatedEndDate);
      var result = await mediator.Send(actualCommand);
      return result.IsSuccess ? Results.Ok() : Results.NotFound(result.Error);
    });

    group.MapDelete("/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var result = await mediator.Send(new DeleteProjectCommand(tenantId, id, tenantId));
      return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
    });

    var spacesGroup = app.MapGroup("/api/v1/spaces").WithTags("Spaces").RequireAuthorization();

    spacesGroup.MapGet("", async (System.Security.Claims.ClaimsPrincipal principal, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var result = await mediator.Send(new GetSpacesQuery(tenantId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    spacesGroup.MapPost("", async (CreateSpaceCommand command, IMediator mediator) =>
    {
      var result = await mediator.Send(command);
      return result.IsSuccess ? Results.Created($"/api/v1/spaces/{result.Value!.Id}", result.Value) : Results.BadRequest(result.Error);
    });

    spacesGroup.MapPatch("/{id:guid}", async (Guid id, UpdateSpaceCommand command, IMediator mediator) =>
    {
      var actualCommand = command with { SpaceId = id };
      var result = await mediator.Send(actualCommand);
      return result.IsSuccess ? Results.Ok() : Results.NotFound(result.Error);
    });

    var foldersGroup = app.MapGroup("/api/v1/folders").WithTags("Folders").RequireAuthorization();

    foldersGroup.MapGet("", async (System.Security.Claims.ClaimsPrincipal principal, Guid spaceId, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var result = await mediator.Send(new GetFoldersQuery(tenantId, spaceId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    foldersGroup.MapPost("", async (CreateFolderCommand command, IMediator mediator) =>
    {
      var result = await mediator.Send(command);
      return result.IsSuccess ? Results.Created($"/api/v1/folders/{result.Value!.Id}", result.Value) : Results.BadRequest(result.Error);
    });

    return app;
  }
}
