using System.Linq;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ticketing.Application.Commands;
using Ticketing.Application.DTOs;
using Ticketing.Application.Queries;
using Ticketing.Infrastructure;

namespace Ticketing.Presentation.Endpoints;

public static class TicketingEndpoints
{
  /// <summary>
  /// Cómo se llama un ticket cuando se habla de él con otro módulo.
  ///
  /// Antes era una cadena escrita a mano aquí, porque Ticketing no puede referenciar a Identity
  /// y los puertos hablan en cadenas: si divergieran, el filtro devolvería una lista vacía **sin
  /// dar ningún error**. Ahora el nombre viene de BuildingBlocks, que sí ven los dos lados. La
  /// prueba de integración que marca un ticket y lo busca por el filtro se queda: vigila la
  /// unión entera, no sólo la cadena.
  /// </summary>
  private const string TipoDeFavoritoTicket = BuildingBlocks.Domain.TiposDeEntidad.Ticket;

  /// <summary>
  /// Las rutas de archivo y papelera, con la acción que ejecuta cada una.
  ///
  /// En una tabla y no en cuatro bloques copiados: los cuatro endpoints son idénticos salvo el
  /// verbo, y copiarlos es como se acaban desincronizando.
  /// </summary>
  private static readonly (string Ruta, BuildingBlocks.Application.AccionDeArchivo Accion)[] AccionesDeArchivo =
  [
    ("archivar", BuildingBlocks.Application.AccionDeArchivo.Archivar),
    ("desarchivar", BuildingBlocks.Application.AccionDeArchivo.Desarchivar),
    ("restaurar", BuildingBlocks.Application.AccionDeArchivo.RestaurarDePapelera)
  ];

  public static IServiceCollection AddTicketingPresentation(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddTicketingInfrastructure(configuration);
    return services;
  }

  public static IEndpointRouteBuilder MapTicketingEndpoints(this IEndpointRouteBuilder app)
  {
    var group = app.MapGroup("/api/v1/tickets").WithTags("Tickets").RequireAuthorization();

    group.MapGet("", async (System.Security.Claims.ClaimsPrincipal principal, [Microsoft.AspNetCore.Mvc.FromQuery] Guid? customerId, [Microsoft.AspNetCore.Mvc.FromQuery] Guid? agentId, [Microsoft.AspNetCore.Mvc.FromQuery] string? priority, [Microsoft.AspNetCore.Mvc.FromQuery] string? status, [Microsoft.AspNetCore.Mvc.FromQuery] string? filter, BuildingBlocks.Application.Abstractions.IAlcanceDeVista alcances, IMediator mediator, [Microsoft.AspNetCore.Mvc.FromQuery] int page = 1, [Microsoft.AspNetCore.Mvc.FromQuery] int pageSize = 25, [Microsoft.AspNetCore.Mvc.FromQuery] string? sortColumn = null, [Microsoft.AspNetCore.Mvc.FromQuery] string? sortDirection = null, [Microsoft.AspNetCore.Mvc.FromQuery] DateTime? startDate = null, [Microsoft.AspNetCore.Mvc.FromQuery] DateTime? endDate = null) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var userId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;

      // El resolutor decide qué hace falta consultar para el filtro pedido —favoritos,
      // compartidos, nada— en un solo sitio. Cuando esto lo hacía cada endpoint, el de tickets
      // pedía los favoritos y el de tareas recibía el mismo parámetro sin pedir nada.
      var alcance = await alcances.ResolverAsync(filter, userId, TipoDeFavoritoTicket);

      var query = new GetTicketsQuery(
          tenantId, customerId, agentId, priority, status,
          new() { Page = page, PageSize = pageSize, SortColumn = sortColumn, SortDirection = sortDirection, StartDate = startDate, EndDate = endDate },
          alcance);

      var result = await mediator.Send(query);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var result = await mediator.Send(new GetTicketByIdQuery(tenantId, id));
      return result.Value is null ? Results.NotFound() : Results.Ok(result.Value);
    });

    group.MapPost("", async (System.Security.Claims.ClaimsPrincipal principal, CreateTicketCommand command, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var userId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value, out var _uid) ? _uid : Guid.Empty;
      
      var actualCommand = command with { TenantId = tenantId, CustomerId = userId };
      var result = await mediator.Send(actualCommand);
      return result.IsSuccess
              ? Results.Created($"/api/v1/tickets/{result.Value!.Id}", result.Value)
              : Results.BadRequest(result.Error);
    });

    group.MapPatch("/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, UpdateTicketCommand command, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var actualCommand = new UpdateTicketCommand(tenantId, id, command.Title, command.Description, command.Priority, command.Status, command.AssignedAgentId);
      var result = await mediator.Send(actualCommand);
      return result.IsSuccess ? Results.Ok() : Results.NotFound(result.Error);
    });

    group.MapPatch("/{id:guid}/status", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, ChangeTicketStatusCommand command, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var actualCommand = new ChangeTicketStatusCommand(tenantId, id, command.NewStatus);
      var result = await mediator.Send(actualCommand);
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    group.MapPatch("/{id:guid}/assign", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, AssignTicketCommand command, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var actualCommand = new AssignTicketCommand(tenantId, id, command.AgentId);
      var result = await mediator.Send(actualCommand);
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    // Borrar un ticket es mandarlo a la papelera.
    //
    // Este endpoint leía el ticket, comprobaba que existía y devolvía 204 **sin borrar nada**:
    // la pantalla decía que se había borrado y el ticket seguía ahí al recargar. Es el mismo
    // patrón que el borrado de tareas, que hacía exactamente lo mismo.
    group.MapDelete("/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;

      var result = await mediator.Send(new Ticketing.Application.CambiarArchivoDeTicketCommand(
          tenantId, id, BuildingBlocks.Application.AccionDeArchivo.EnviarAPapelera));

      return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
    });

    // Archivar, desarchivar y restaurar. Cuatro acciones, una ruta por cada una: son verbos
    // distintos y meterlos en un PATCH con un campo «accion» obligaría a la pantalla a componer
    // un cuerpo para decir algo que ya dice la URL.
    foreach (var (ruta, accion) in AccionesDeArchivo)
    {
      group.MapPost("/{id:guid}/" + ruta, async (System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator) =>
      {
        var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;

        var result = await mediator.Send(new Ticketing.Application.CambiarArchivoDeTicketCommand(tenantId, id, accion));
        return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
      });
    }

    return app;
  }
}
