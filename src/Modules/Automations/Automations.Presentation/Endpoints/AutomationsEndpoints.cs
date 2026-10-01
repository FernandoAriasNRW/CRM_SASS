using BuildingBlocks.Application.Abstractions;
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
  private const string NotFound = "Automatización no encontrada";

  public static IServiceCollection AddAutomationsPresentation(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddAutomationsInfrastructure(configuration);
    return services;
  }

  public static IEndpointRouteBuilder MapAutomationsEndpoints(this IEndpointRouteBuilder app)
  {
    var group = app.MapGroup("/api/v1/automations").WithTags("Automations").RequireAuthorization();

    static IResult Respond(bool success, string? error)
    {
      if (success) return Results.Ok();

      // Un valor que el dominio rechaza no es una regla que no existe: devolver 404 mandaría a
      // buscar el fallo donde no está.
      return error == NotFound ? Results.NotFound(error) : Results.BadRequest(error);
    }

    /// El vocabulario que la interfaz necesita para construir el formulario. Va servido y no
    /// repetido en el cliente: una lista duplicada se desincroniza el día que se añada un
    /// disparador, y entonces se puede configurar algo que el servidor no entiende.
    group.MapGet("/vocabulary", () => Results.Ok(new
    {
      triggers = TriggerTypes.All(),
      fields = EventFields.All(),
      operators = ConditionOperators.All(),
      actions = ActionTypes.All(),

      // Qué es numérico, servido en vez de deducido en el cliente.
      //
      // La interfaz necesita saberlo para no ofrecer «menor o igual» sobre un campo de texto —el
      // dominio lo rechaza y quien lo intente sólo vería un error—. Repetir la lista allí sería
      // una copia que se desincroniza el día que se añada un campo numérico, que es justo lo que
      // acaba de pasar con los tipos de informe: el desplegable ofrecía dos que el servidor no
      // conocía y la mitad del formulario no funcionaba.
      numericFields = EventFields.All().Where(EventFields.IsNumeric),
      numericOperators = ConditionOperators.All().Where(ConditionOperators.IsNumeric),
      valuelessOperators = ConditionOperators.All().Where(o => !ConditionOperators.NeedsValue(o)),
      timeTriggers = TriggerTypes.All().Where(TriggerTypes.IsTimeBased),

      // El valor especial de «Notificar» para avisar a quien tenga la tarea.
      assigneeRecipient = ActionTypes.AssigneeRecipient,
    }));

    group.MapGet("", async (IUserContext currentUser, IMediator mediator) =>
    {
      var result = await mediator.Send(new GetAutomationRulesQuery(currentUser.TenantId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapPost("", async (IUserContext currentUser, DefineAutomationRuleCommand command, IMediator mediator) =>
    {
      var result = await mediator.Send(command with { TenantId = currentUser.TenantId });

      return result.IsSuccess
          ? Results.Created($"/api/v1/automations/{result.Value!.Id}", result.Value)
          : Results.BadRequest(result.Error);
    });

    group.MapPut("/{id:guid}", async (IUserContext currentUser, Guid id, UpdateAutomationRuleCommand command, IMediator mediator) =>
    {
      var result = await mediator.Send(command with { TenantId = currentUser.TenantId, Id = id });
      return Respond(result.IsSuccess, result.Error);
    });

    // Activar y desactivar tiene endpoint propio porque es la operación que se hace con prisa,
    // cuando una automatización está haciendo daño: obligar a reenviar la regla entera para
    // apagarla sería pedir precisión en el peor momento.
    group.MapPut("/{id:guid}/active", async (IUserContext currentUser, Guid id, SetAutomationRuleActiveCommand command, IMediator mediator) =>
    {
      var result = await mediator.Send(command with { TenantId = currentUser.TenantId, Id = id });
      return Respond(result.IsSuccess, result.Error);
    });

    group.MapDelete("/{id:guid}", async (IUserContext currentUser, Guid id, IMediator mediator) =>
    {
      var result = await mediator.Send(new RemoveAutomationRuleCommand(currentUser.TenantId, id));
      return result.IsSuccess ? Results.NoContent() : Respond(false, result.Error);
    });

    // El historial de una regla: qué hizo, cuándo y sobre qué.
    //
    // Es lo que se mira cuando alguien dice «mi automatización no funciona», y responde la
    // pregunta que el contador de la regla no sabía contestar: si no salta, o si salta y las
    // condiciones no se cumplen.
    group.MapGet("/{id:guid}/executions", async (
        IUserContext currentUser, Guid id, IMediator mediator, int count = 20) =>
    {
      var result = await mediator.Send(new GetExecutionsQuery(currentUser.TenantId, id, count));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    return app;
  }
}
