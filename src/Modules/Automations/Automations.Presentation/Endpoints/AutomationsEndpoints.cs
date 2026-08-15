using System.Security.Claims;
using Automations.Application;
using Automations.Domain.ValueObjects;
using Automations.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Automations.Presentation.Endpoints;

public static class AutomationsEndpoints
{
  private const string NoEncontrada = "Automatización no encontrada";

  public static IServiceCollection AddAutomationsPresentation(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddAutomationsInfrastructure(configuration);
    return services;
  }

  public static IEndpointRouteBuilder MapAutomationsEndpoints(this IEndpointRouteBuilder app)
  {
    var group = app.MapGroup("/api/v1/automations").WithTags("Automations").RequireAuthorization();

    static Guid TenantDe(ClaimsPrincipal principal)
        => Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var id) ? id : Guid.Empty;

    static IResult Responder(bool exito, string? error)
    {
      if (exito) return Results.Ok();

      // Un valor que el dominio rechaza no es una regla que no existe: devolver 404 mandaría a
      // buscar el fallo donde no está.
      return error == NoEncontrada ? Results.NotFound(error) : Results.BadRequest(error);
    }

    /// El vocabulario que la interfaz necesita para construir el formulario. Va servido y no
    /// repetido en el cliente: una lista duplicada se desincroniza el día que se añada un
    /// disparador, y entonces se puede configurar algo que el servidor no entiende.
    group.MapGet("/vocabulario", () => Results.Ok(new
    {
      disparadores = TipoDeDisparador.Todos(),
      campos = CampoDelEvento.Todos(),
      operadores = Operador.Todos(),
      acciones = TipoDeAccion.Todos(),
    }));

    group.MapGet("", async (ClaimsPrincipal principal, IMediator mediator) =>
    {
      var result = await mediator.Send(new GetAutomationRulesQuery(TenantDe(principal)));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapPost("", async (ClaimsPrincipal principal, DefineAutomationRuleCommand command, IMediator mediator) =>
    {
      var result = await mediator.Send(command with { TenantId = TenantDe(principal) });

      return result.IsSuccess
          ? Results.Created($"/api/v1/automations/{result.Value!.Id}", result.Value)
          : Results.BadRequest(result.Error);
    });

    group.MapPut("/{id:guid}", async (ClaimsPrincipal principal, Guid id, UpdateAutomationRuleCommand command, IMediator mediator) =>
    {
      var result = await mediator.Send(command with { TenantId = TenantDe(principal), Id = id });
      return Responder(result.IsSuccess, result.Error);
    });

    // Activar y desactivar tiene endpoint propio porque es la operación que se hace con prisa,
    // cuando una automatización está haciendo daño: obligar a reenviar la regla entera para
    // apagarla sería pedir precisión en el peor momento.
    group.MapPut("/{id:guid}/active", async (ClaimsPrincipal principal, Guid id, SetAutomationRuleActiveCommand command, IMediator mediator) =>
    {
      var result = await mediator.Send(command with { TenantId = TenantDe(principal), Id = id });
      return Responder(result.IsSuccess, result.Error);
    });

    group.MapDelete("/{id:guid}", async (ClaimsPrincipal principal, Guid id, IMediator mediator) =>
    {
      var result = await mediator.Send(new RemoveAutomationRuleCommand(TenantDe(principal), id));
      return result.IsSuccess ? Results.NoContent() : Responder(false, result.Error);
    });

    return app;
  }
}
