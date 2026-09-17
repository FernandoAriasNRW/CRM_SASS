using BuildingBlocks.Application.Abstractions;
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
  private const string TipoDeFavoritoTicket = BuildingBlocks.Domain.EntityTypes.Ticket;

  /// <summary>
  /// Las rutas de archivo y papelera, con la acción que ejecuta cada una.
  ///
  /// En una tabla y no en cuatro bloques copiados: los cuatro endpoints son idénticos salvo el
  /// verbo, y copiarlos es como se acaban desincronizando.
  /// </summary>
  private static readonly (string Ruta, BuildingBlocks.Application.ArchiveAction Accion)[] AccionesDeArchivo =
  [
    ("archivar", BuildingBlocks.Application.ArchiveAction.Archive),
    ("desarchivar", BuildingBlocks.Application.ArchiveAction.Unarchive),
    ("restaurar", BuildingBlocks.Application.ArchiveAction.RestoreFromTrash)
  ];

  public static IServiceCollection AddTicketingPresentation(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddTicketingInfrastructure(configuration);
    return services;
  }

  public static IEndpointRouteBuilder MapTicketingEndpoints(this IEndpointRouteBuilder app)
  {
    var group = app.MapGroup("/api/v1/tickets").WithTags("Tickets").RequireAuthorization();

    group.MapGet("", async (IUserContext currentUser, [Microsoft.AspNetCore.Mvc.FromQuery] Guid? customerId, [Microsoft.AspNetCore.Mvc.FromQuery] Guid? agentId, [Microsoft.AspNetCore.Mvc.FromQuery] string? priority, [Microsoft.AspNetCore.Mvc.FromQuery] string? status, [Microsoft.AspNetCore.Mvc.FromQuery] string? filter, BuildingBlocks.Application.Abstractions.IViewScopeResolver alcances, IMediator mediator, [Microsoft.AspNetCore.Mvc.FromQuery] int page = 1, [Microsoft.AspNetCore.Mvc.FromQuery] int pageSize = 25, [Microsoft.AspNetCore.Mvc.FromQuery] string? sortColumn = null, [Microsoft.AspNetCore.Mvc.FromQuery] string? sortDirection = null, [Microsoft.AspNetCore.Mvc.FromQuery] DateTime? startDate = null, [Microsoft.AspNetCore.Mvc.FromQuery] DateTime? endDate = null, [Microsoft.AspNetCore.Mvc.FromQuery] string? search = null) =>
    {
      var tenantId = currentUser.TenantId;
      var userId = currentUser.UserId;

      // El resolutor decide qué hace falta consultar para el filtro pedido —favoritos,
      // compartidos, nada— en un solo sitio. Cuando esto lo hacía cada endpoint, el de tickets
      // pedía los favoritos y el de tareas recibía el mismo parámetro sin pedir nada.
      var alcance = await alcances.ResolveAsync(filter, userId, TipoDeFavoritoTicket);

      var query = new GetTicketsQuery(
          tenantId, customerId, agentId, priority, status,
          new() { Page = page, PageSize = pageSize, SortColumn = sortColumn, SortDirection = sortDirection, StartDate = startDate, EndDate = endDate, Search = search },
          alcance);

      var result = await mediator.Send(query);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/{id:guid}", async (IUserContext currentUser, Guid id, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var result = await mediator.Send(new GetTicketByIdQuery(tenantId, id));
      return result.Value is null ? Results.NotFound() : Results.Ok(result.Value);
    });

    group.MapPost("", async (IUserContext currentUser, CreateTicketCommand command, IMediator mediator) =>
    {
      var actualCommand = command with { TenantId = currentUser.TenantId, CustomerId = currentUser.UserId };
      var result = await mediator.Send(actualCommand);
      return result.IsSuccess
              ? Results.Created($"/api/v1/tickets/{result.Value!.Id}", result.Value)
              : Results.BadRequest(result.Error);
    });

    group.MapPatch("/{id:guid}", async (IUserContext currentUser, Guid id, UpdateTicketCommand command, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var actualCommand = command with { TenantId = tenantId, TicketId = id };
      var result = await mediator.Send(actualCommand);
      if (result.IsSuccess) return Results.Ok();

      // Un 404 sólo cuando el ticket no existe. Antes cualquier fallo salía como «no encontrado»,
      // así que un título de tres letras o una prioridad mal escrita se leían como si el ticket
      // hubiera desaparecido, y la pantalla no tenía forma de explicar qué corregir.
      return result.Error == "Ticket not found"
          ? Results.NotFound(result.Error)
          : Results.BadRequest(result.Error);
    });

    group.MapPatch("/{id:guid}/status", async (IUserContext currentUser, Guid id, ChangeTicketStatusCommand command, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var actualCommand = new ChangeTicketStatusCommand(tenantId, id, command.NewStatus);
      var result = await mediator.Send(actualCommand);
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    group.MapPatch("/{id:guid}/assign", async (IUserContext currentUser, Guid id, AssignTicketCommand command, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var actualCommand = new AssignTicketCommand(tenantId, id, command.AgentId);
      var result = await mediator.Send(actualCommand);
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
    });

    // Borrar un ticket es mandarlo a la papelera.
    //
    // Este endpoint leía el ticket, comprobaba que existía y devolvía 204 **sin borrar nada**:
    // la pantalla decía que se había borrado y el ticket seguía ahí al recargar. Es el mismo
    // patrón que el borrado de tareas, que hacía exactamente lo mismo.
    group.MapDelete("/{id:guid}", async (IUserContext currentUser, Guid id, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;

      var result = await mediator.Send(new Ticketing.Application.CambiarArchivoDeTicketCommand(
          tenantId, id, BuildingBlocks.Application.ArchiveAction.MoveToTrash));

      return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
    });

    // Archivar, desarchivar y restaurar. Cuatro acciones, una ruta por cada una: son verbos
    // distintos y meterlos en un PATCH con un campo «accion» obligaría a la pantalla a componer
    // un cuerpo para decir algo que ya dice la URL.
    foreach (var (ruta, accion) in AccionesDeArchivo)
    {
      group.MapPost("/{id:guid}/" + ruta, async (IUserContext currentUser, Guid id, IMediator mediator) =>
      {
        var tenantId = currentUser.TenantId;

        var result = await mediator.Send(new Ticketing.Application.CambiarArchivoDeTicketCommand(tenantId, id, accion));
        return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
      });
    }

    MapEntradaDeTickets(app);

    return app;
  }

  /// <summary>Cabecera en la que llega la clave de entrada.</summary>
  public const string CabeceraDeClave = "X-Api-Key";

  /// <summary>
  /// La política de CORS del endpoint público. La registra el host: la entrada se llama desde
  /// páginas de clientes cuyo dominio la aplicación no conoce.
  /// </summary>
  public const string PoliticaCorsDeEntrada = "EntradaDeTickets";

  /// <summary>Política de límite de peticiones del endpoint público. La registra el host.</summary>
  public const string LimiteDeEntrada = "entrada-de-tickets";

  /// <summary>
  /// Tickets que llegan desde fuera de la aplicación, y las claves con las que llegan.
  ///
  /// El endpoint público no pide sesión: lo llama el formulario de soporte de la web de un
  /// cliente, o su backend, con una clave de entrada en <see cref="CabeceraDeClave"/>. La clave
  /// sólo sirve para crear tickets y fija la organización. Ver <c>ClaveDeEntrada</c>.
  ///
  /// Las claves las gestiona un administrador: quien las tiene puede escribir en la bandeja de
  /// tickets de la organización.
  /// </summary>
  private static void MapEntradaDeTickets(IEndpointRouteBuilder app)
  {
    // Admite JSON, para un backend, y multipart/form-data, para un formulario con adjuntos. Los
    // campos se llaman igual en los dos; los ficheros van en «attachments», uno o varios.
    app.MapPost("/api/v1/entrada/tickets", async (HttpContext http, IMediator mediator, CancellationToken ct) =>
    {
      var peticion = await LeerPeticionExternaAsync(http, ct);
      if (peticion is null)
        return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "El cuerpo no es JSON ni un formulario válido");

      var result = await mediator.Send(peticion with { Clave = http.Request.Headers[CabeceraDeClave].ToString() }, ct);

      if (result.IsSuccess)
        return Results.Created($"/api/v1/tickets/{result.Value!.Id}", result.Value);

      return result.Error == Ticketing.Application.Entrada.ErroresDeEntrada.ClaveNoValida
          ? Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: result.Error)
          : Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Error);
    })
    .AllowAnonymous()
    .RequireCors(PoliticaCorsDeEntrada)
    .RequireRateLimiting(LimiteDeEntrada)
    .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(Ticketing.Domain.Entities.ReglasDeAdjuntos.MaximoDeLaPeticion))
    .WithTags("Entrada de tickets");

    // Adjuntar desde la ficha del ticket, con sesión. Las mismas reglas que desde fuera.
    app.MapGet("/api/v1/tickets/{id:guid}/adjuntos", async (Guid id, BuildingBlocks.Application.Abstractions.IUserContext usuario, IMediator mediator) =>
    {
      var result = await mediator.Send(new Ticketing.Application.Entrada.GetAdjuntosDeTicketQuery(usuario.TenantId, id));
      return Results.Ok(result.Value);
    })
    .RequireAuthorization()
    .WithTags("Tickets");

    app.MapPost("/api/v1/tickets/{id:guid}/adjuntos", async (Guid id, HttpContext http, BuildingBlocks.Application.Abstractions.IUserContext usuario, IMediator mediator, CancellationToken ct) =>
    {
      if (!http.Request.HasFormContentType)
        return Results.BadRequest("Los adjuntos se envían como multipart/form-data");

      var formulario = await http.Request.ReadFormAsync(ct);
      var result = await mediator.Send(new Ticketing.Application.Entrada.SubirAdjuntosDeTicketCommand(
          id, usuario.UserId, Ficheros(formulario)), ct);

      if (result.IsSuccess) return Results.Ok(result.Value);
      return result.Error == Ticketing.Application.Entrada.ErroresDeEntrada.TicketNoEncontrado
          ? Results.NotFound(result.Error)
          : Results.BadRequest(result.Error);
    })
    .RequireAuthorization()
    .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(Ticketing.Domain.Entities.ReglasDeAdjuntos.MaximoDeLaPeticion))
    .WithTags("Tickets");

    var claves = app.MapGroup("/api/v1/tickets/claves-de-entrada")
        .WithTags("Entrada de tickets")
        .RequireAuthorization(politica => politica.RequireRole("Admin"));

    claves.MapGet("", async (BuildingBlocks.Application.Abstractions.IUserContext usuario, IMediator mediator) =>
    {
      var result = await mediator.Send(new Ticketing.Application.Entrada.GetClavesDeEntradaQuery(usuario.TenantId));
      return Results.Ok(result.Value);
    });

    claves.MapPost("", async (CrearClaveDeEntradaRequest req, BuildingBlocks.Application.Abstractions.IUserContext usuario, IMediator mediator) =>
    {
      var result = await mediator.Send(new Ticketing.Application.Entrada.CrearClaveDeEntradaCommand(usuario.TenantId, usuario.UserId, req.Nombre ?? string.Empty));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    claves.MapDelete("/{id:guid}", async (Guid id, BuildingBlocks.Application.Abstractions.IUserContext usuario, IMediator mediator) =>
    {
      var result = await mediator.Send(new Ticketing.Application.Entrada.RevocarClaveDeEntradaCommand(usuario.TenantId, id));
      return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
    });
  }
  /// <summary>
  /// Lee la petición externa sea JSON o formulario. Devuelve <c>null</c> si no es ninguno de los
  /// dos o no se puede leer; la clave la pone quien llama, desde la cabecera.
  /// </summary>
  private static async Task<Ticketing.Application.Entrada.CrearTicketExternoCommand?> LeerPeticionExternaAsync(HttpContext http, CancellationToken ct)
  {
    try
    {
      if (http.Request.HasFormContentType)
      {
        var f = await http.Request.ReadFormAsync(ct);
        string? Campo(string nombre) => f.TryGetValue(nombre, out var v) ? v.ToString() : null;

        // Las etiquetas pueden llegar repetidas (tags=a&tags=b) o juntas con comas.
        var etiquetas = f.TryGetValue("tags", out var t)
            ? t.SelectMany(x => (x ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToList()
            : new List<string>();

        return new(string.Empty, Campo("title"), Campo("description"), Campo("requesterName"), Campo("requesterEmail"),
            Campo("requesterPhone"), Campo("requesterCompany"), Campo("priority"), Campo("status"), Campo("classification"),
            Guid.TryParse(Campo("teamId"), out var equipo) ? equipo : null, etiquetas, Ficheros(f));
      }

      var cuerpo = await http.Request.ReadFromJsonAsync<TicketExternoRequest>(ct);
      if (cuerpo is null) return null;

      return new(string.Empty, cuerpo.Title, cuerpo.Description, cuerpo.RequesterName, cuerpo.RequesterEmail,
          cuerpo.RequesterPhone, cuerpo.RequesterCompany, cuerpo.Priority, cuerpo.Status, cuerpo.Classification,
          cuerpo.TeamId, cuerpo.Tags ?? [], []);
    }
    catch (Exception e) when (e is System.Text.Json.JsonException or InvalidDataException or BadHttpRequestException)
    {
      return null;
    }
  }

  private static List<Ticketing.Application.Entrada.FicheroRecibido> Ficheros(IFormCollection formulario)
      => formulario.Files
          .Where(f => f.Name is "attachments" or "attachments[]")
          .Select(f => new Ticketing.Application.Entrada.FicheroRecibido(
              Path.GetFileName(f.FileName), f.ContentType ?? string.Empty, f.Length, f.OpenReadStream))
          .ToList();
}

/// <summary>
/// El cuerpo del endpoint público, en inglés como el resto de la API: lo van a escribir
/// integradores de fuera, y los nombres son los que ya tienen los tickets.
/// </summary>
public sealed record TicketExternoRequest(
    string? Title,
    string? Description,
    string? RequesterName,
    string? RequesterEmail,
    string? RequesterPhone,
    string? RequesterCompany,
    string? Priority,
    string? Status,
    string? Classification,
    Guid? TeamId,
    List<string>? Tags);

public sealed record CrearClaveDeEntradaRequest(string? Nombre);
