using System.Security.Claims;
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

    static Guid TenantDe(ClaimsPrincipal principal)
        => Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var id) ? id : Guid.Empty;

    // Definiciones
    group.MapGet("", async (ClaimsPrincipal principal, IMediator mediator, [FromQuery] string? entidad) =>
    {
      var result = await mediator.Send(new GetCustomFieldsQuery(TenantDe(principal), entidad));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapPost("", async (ClaimsPrincipal principal, DefineCustomFieldCommand command, IMediator mediator) =>
    {
      var result = await mediator.Send(command with { TenantId = TenantDe(principal) });
      return result.IsSuccess
          ? Results.Created($"/api/v1/custom-fields/{result.Value!.Id}", result.Value)
          : Results.BadRequest(result.Error);
    });

    group.MapPut("/{id:guid}", async (ClaimsPrincipal principal, Guid id, UpdateCustomFieldCommand command, IMediator mediator) =>
    {
      var result = await mediator.Send(command with { TenantId = TenantDe(principal), Id = id });
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    group.MapDelete("/{id:guid}", async (ClaimsPrincipal principal, Guid id, IMediator mediator) =>
    {
      var result = await mediator.Send(new RemoveCustomFieldCommand(TenantDe(principal), id));
      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
    });

    // Valores de una entidad concreta. Devuelve todas las definiciones que aplican, con valor o
    // sin él: un campo recién creado tiene que aparecer en el formulario aunque nadie lo haya
    // rellenado todavía.
    group.MapGet("/values/{entidad}/{entityId:guid}", async (ClaimsPrincipal principal, string entidad, Guid entityId, IMediator mediator) =>
    {
      var result = await mediator.Send(new GetCustomFieldValuesQuery(TenantDe(principal), entidad, entityId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapPut("/values/{definitionId:guid}/{entityId:guid}", async (ClaimsPrincipal principal, Guid definitionId, Guid entityId, SetCustomFieldValueCommand command, IMediator mediator) =>
    {
      var result = await mediator.Send(new SetCustomFieldValueCommand(
          TenantDe(principal), definitionId, entityId, command.Valor));

      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    return app;
  }
}
