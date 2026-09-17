using BuildingBlocks.Application.Abstractions;
using CustomFields.Application.Commands;
using CustomFields.Application.Queries;
using CustomFields.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CustomFields.Presentation.Endpoints;

public static class CustomFieldsEndpoints
{
  public static IServiceCollection AddCustomFieldsPresentation(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddCustomFieldsInfrastructure(configuration);
    return services;
  }

  public static IEndpointRouteBuilder MapCustomFieldsEndpoints(this IEndpointRouteBuilder app)
  {
    var group = app.MapGroup("/api/v1/custom-fields").WithTags("CustomFields").RequireAuthorization();

    // Definiciones
    group.MapGet("", async (IUserContext currentUser, IMediator mediator, [FromQuery] string? entidad) =>
    {
      var result = await mediator.Send(new GetCustomFieldsQuery(currentUser.TenantId, entidad));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapPost("", async (IUserContext currentUser, DefineCustomFieldCommand command, IMediator mediator) =>
    {
      var result = await mediator.Send(command with { TenantId = currentUser.TenantId });
      return result.IsSuccess
          ? Results.Created($"/api/v1/custom-fields/{result.Value!.Id}", result.Value)
          : Results.BadRequest(result.Error);
    });

    group.MapPut("/{id:guid}", async (IUserContext currentUser, Guid id, UpdateCustomFieldCommand command, IMediator mediator) =>
    {
      var result = await mediator.Send(command with { TenantId = currentUser.TenantId, Id = id });
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    group.MapDelete("/{id:guid}", async (IUserContext currentUser, Guid id, IMediator mediator) =>
    {
      var result = await mediator.Send(new RemoveCustomFieldCommand(currentUser.TenantId, id));
      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
    });

    // Valores de una entidad concreta. Devuelve todas las definiciones que aplican, con valor o
    // sin él: un campo recién creado tiene que aparecer en el formulario aunque nadie lo haya
    // rellenado todavía.
    group.MapGet("/values/{entidad}/{entityId:guid}", async (IUserContext currentUser, string entidad, Guid entityId, IMediator mediator) =>
    {
      var result = await mediator.Send(new GetCustomFieldValuesQuery(currentUser.TenantId, entidad, entityId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapPut("/values/{definitionId:guid}/{entityId:guid}", async (IUserContext currentUser, Guid definitionId, Guid entityId, SetCustomFieldValueCommand command, IMediator mediator) =>
    {
      var result = await mediator.Send(new SetCustomFieldValueCommand(
          currentUser.TenantId, definitionId, entityId, command.Valor));

      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    return app;
  }
}
