using System.Linq;
using BuildingBlocks.Application.Abstractions;
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
  /// <summary>Las rutas de archivo y papelera, con la acción que ejecuta cada una.</summary>
  private static readonly (string Ruta, BuildingBlocks.Application.ArchiveAction Accion)[] ArchiveActions =
  [
    ("archive", BuildingBlocks.Application.ArchiveAction.Archive),
    ("unarchive", BuildingBlocks.Application.ArchiveAction.Unarchive),
    ("restore", BuildingBlocks.Application.ArchiveAction.RestoreFromTrash)
  ];

  public static IServiceCollection AddProjectsPresentation(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddProjectsInfrastructure(configuration);
    return services;
  }

  public static IEndpointRouteBuilder MapProjectsEndpoints(this IEndpointRouteBuilder app)
  {
    var group = app.MapGroup("/api/v1/projects").WithTags("Projects").RequireAuthorization();

    group.MapGet("", async (IUserContext currentUser, [Microsoft.AspNetCore.Mvc.FromQuery] string? status, [Microsoft.AspNetCore.Mvc.FromQuery] Guid? ownerId, [Microsoft.AspNetCore.Mvc.FromQuery] Guid? spaceId, [Microsoft.AspNetCore.Mvc.FromQuery] Guid? folderId, [Microsoft.AspNetCore.Mvc.FromQuery] string? filter, BuildingBlocks.Application.Abstractions.IViewScopeResolver viewScopes, IMediator mediator, [Microsoft.AspNetCore.Mvc.FromQuery] int page = 1, [Microsoft.AspNetCore.Mvc.FromQuery] int pageSize = 25, [Microsoft.AspNetCore.Mvc.FromQuery] string? search = null) =>
    {
      var tenantId = currentUser.TenantId;
      var userId = currentUser.UserId;
      // El resolutor decide en un solo sitio qué hace falta consultar para el filtro pedido.
      var viewScope = await viewScopes.ResolveAsync(filter, userId, BuildingBlocks.Domain.EntityTypes.Project);

      var result = await mediator.Send(new GetProjectsQuery(tenantId, status, ownerId, spaceId, folderId, viewScope, new() { Page = page, PageSize = pageSize, Search = search }));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/{id:guid}", async (IUserContext currentUser, Guid id, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var result = await mediator.Send(new GetProjectByIdQuery(tenantId, id));
      return result.Value is null ? Results.NotFound() : Results.Ok(result.Value);
    });

    // El tenant y el dueño salen del token, nunca del cuerpo.
    //
    // Antes esto era `mediator.Send(command)` con el comando enlazado tal cual del JSON, y el
    // comando lleva `TenantId` y `OwnerId` dentro. Dos consecuencias, una peor que la otra:
    //
    //  - Quien no los mandaba creaba el proyecto con `Guid.Empty`, así que el filtro global de
    //    inquilino no volvía a verlo nunca. El alta respondía 201 y el proyecto era invisible
    //    en la lista inmediatamente después. De ahí que el dashboard contara siempre cero.
    //  - Quien sí los mandaba **elegía en qué organización escribir**. Cualquier usuario
    //    autenticado podía plantar datos en el inquilino de otro poniendo un Guid en el JSON.
    //
    // `IUserContext` es la abstracción que ya existía para esto, y busca el claim sin distinguir
    // mayúsculas: no se repite aquí el parseo a mano que en su día dejó el tenant vacío.
    group.MapPost("", async (CreateProjectCommand command, IUserContext user, IMediator mediator) =>
    {
      var result = await mediator.Send(command with
      {
        TenantId = user.TenantId,
        OwnerId = user.UserId,
      });

      return result.IsSuccess
              ? Results.Created($"/api/v1/projects/{result.Value!.Id}", result.Value)
              : Results.BadRequest(result.Error);
    });

    group.MapPatch("/{id:guid}", async (IUserContext currentUser, Guid id, PatchProjectCommand command, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var actualCommand = new PatchProjectCommand(tenantId, id, command.Name, command.Description, command.Status, command.EstimatedEndDate);
      var result = await mediator.Send(actualCommand);
      return result.IsSuccess ? Results.Ok() : Results.NotFound(result.Error);
    });

    group.MapDelete("/{id:guid}", async (IUserContext currentUser, Guid id, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var actorId = currentUser.UserId;

      // Quien borra es la persona, no el inquilino. Aquí iba el TenantId en el hueco de
      // «DeletedBy», así que el registro de quién borró un proyecto decía el nombre de la
      // empresa en todas las filas.
      var result = await mediator.Send(new DeleteProjectCommand(tenantId, id, actorId));
      return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
    });

    // Archivar, desarchivar y restaurar de la papelera. En una tabla y no en tres bloques
    // copiados: son idénticos salvo el verbo.
    foreach (var (route, action) in ArchiveActions)
    {
      group.MapPost("/{id:guid}/" + route, async (IUserContext currentUser, Guid id, IMediator mediator) =>
      {
        var tenantId = currentUser.TenantId;
        var actorId = currentUser.UserId;

        var result = await mediator.Send(
            new Projects.Application.ChangeProjectArchiveStateCommand(tenantId, id, actorId, action));

        return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
      });
    }

    var spacesGroup = app.MapGroup("/api/v1/spaces").WithTags("Spaces").RequireAuthorization();

    spacesGroup.MapGet("", async (IUserContext currentUser, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var result = await mediator.Send(new GetSpacesQuery(tenantId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    spacesGroup.MapPost("", async (CreateSpaceCommand command, IUserContext user, IMediator mediator) =>
    {
      var result = await mediator.Send(command with { TenantId = user.TenantId });
      return result.IsSuccess ? Results.Created($"/api/v1/spaces/{result.Value!.Id}", result.Value) : Results.BadRequest(result.Error);
    });

    // El identificador va en la ruta y el inquilino en el token: del cuerpo no se acepta
    // ninguno de los dos. Si no, se podría renombrar el espacio de otra organización.
    spacesGroup.MapPatch("/{id:guid}", async (Guid id, UpdateSpaceCommand command, IUserContext user, IMediator mediator) =>
    {
      var result = await mediator.Send(command with { SpaceId = id, TenantId = user.TenantId });
      return result.IsSuccess ? Results.Ok() : Results.NotFound(result.Error);
    });

    var foldersGroup = app.MapGroup("/api/v1/folders").WithTags("Folders").RequireAuthorization();

    foldersGroup.MapGet("", async (IUserContext currentUser, Guid spaceId, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var result = await mediator.Send(new GetFoldersQuery(tenantId, spaceId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    foldersGroup.MapPost("", async (CreateFolderCommand command, IUserContext user, IMediator mediator) =>
    {
      var result = await mediator.Send(command with { TenantId = user.TenantId });
      return result.IsSuccess ? Results.Created($"/api/v1/folders/{result.Value!.Id}", result.Value) : Results.BadRequest(result.Error);
    });

    return app;
  }
}
