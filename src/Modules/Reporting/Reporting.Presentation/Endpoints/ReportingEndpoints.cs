using BuildingBlocks.Application.Abstractions;
using System.Linq;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Commands;
using Reporting.Application.Definitions;
using Reporting.Application.Exports;
using Reporting.Application.Schedules;
using Reporting.Application.Queries;
using Reporting.Infrastructure;

namespace Reporting.Presentation.Endpoints;

public static class ReportingEndpoints
{
  public static IServiceCollection AddReportingPresentation(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddReportingInfrastructure(configuration);
    return services;
  }

  public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder app)
  {
    var group = app.MapGroup("/api/v1/reports").WithTags("Reporting").RequireAuthorization();

    group.MapGet("", async (IUserContext currentUser, string? type, IMediator mediator, int page = 1, int pageSize = 25) =>
    {
      var tenantId = currentUser.TenantId;
      var query = new GetReportsQuery(tenantId, type, new() { Page = page, PageSize = pageSize });
      var result = await mediator.Send(query);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/{id:guid}", async (IUserContext currentUser, Guid id, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var result = await mediator.Send(new GetReportByIdQuery(tenantId, id));
      return result.Value is null ? Results.NotFound() : Results.Ok(result.Value);
    });

    group.MapPost("", async (IUserContext currentUser, CreateReportCommand command, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var userId = currentUser.UserId;
      
      var cmdWithClaims = command with { TenantId = tenantId, CreatedById = userId };
      var result = await mediator.Send(cmdWithClaims);
      return result.IsSuccess
              ? Results.Created($"/api/v1/reports/{result.Value!.Id}", result.Value)
              : Results.BadRequest(result.Error);
    });

    // Pedir una exportación. Devuelve 202 y el trabajo con su estado.
    //
    // **202 y no 200 a propósito**: aquí no hay fichero todavía, sólo una promesa de que lo
    // habrá. Con 200 la pantalla podría creer que ya puede descargar, que es lo que hacía el
    // endpoint anterior: llamaba a `MarkAsGenerated` con una URL inventada
    // —`/reports/{id}/{nombre}.pdf`— que no apuntaba a ningún fichero y que ningún endpoint
    // servía. La pantalla decía «generado» y no había nada que descargar.
    group.MapPost("/{id:guid}/export", async (
        IUserContext currentUser, Guid id, string format, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var userId = currentUser.UserId;

      var result = await mediator.Send(new RequestExportCommand(tenantId, id, userId, format));

      return result.IsSuccess
          ? Results.Accepted($"/api/v1/exports/{result.Value!.Id}", result.Value)
          : Results.BadRequest(result.Error);
    });

    // La ruta antigua, que se mantiene para no romper la pantalla mientras se actualiza.
    //
    // Hace **lo mismo** que la nueva —encolar una exportación de verdad— en vez de lo que hacía
    // antes. Se conserva el nombre, no el comportamiento: fingir que se generó algo era el fallo.
    group.MapPost("/{id:guid}/generate", async (
        IUserContext currentUser, Guid id, string format, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var userId = currentUser.UserId;

      var result = await mediator.Send(new RequestExportCommand(tenantId, id, userId, format));

      return result.IsSuccess
          ? Results.Accepted($"/api/v1/exports/{result.Value!.Id}", result.Value)
          : Results.BadRequest(result.Error);
    });

    // ── El constructor de informes ──────────────────────────────────────────────────────────
    //
    // El catálogo va primero y sin autenticar por inquilino porque no depende de ninguno: es la
    // lista de lo que el motor sabe hacer. La pantalla se alimenta de aquí y no escribe ninguna
    // de estas opciones por su cuenta, que es lo que evita volver a ofrecer algo que el servidor
    // no conoce.
    group.MapGet("/catalog", async (IMediator mediator) =>
    {
      var result = await mediator.Send(new GetCatalogQuery());
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    // La vista previa: enseña el resultado **antes** de guardar. Sin ella, construir un informe
    // es escribir a ciegas y descubrir el resultado al exportarlo.
    group.MapPost("/preview", async (
        IUserContext currentUser, PreviewRequest body, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;

      var result = await mediator.Send(new PreviewQuery(
          tenantId, body.Title ?? "Vista previa", body.Definition));

      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/{id:guid}/definition", async (
        IUserContext currentUser, Guid id, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var result = await mediator.Send(new GetDefinitionQuery(tenantId, id));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
    });

    // PUT y no POST: guardar la definición dos veces deja el mismo informe. Con POST, la segunda
    // llamada tendría que decidir si es un conflicto, y no lo es.
    group.MapPut("/{id:guid}/definition", async (
        IUserContext currentUser, Guid id,
        Reporting.Domain.Definitions.ReportDefinition definition, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;

      var result = await mediator.Send(new SaveDefinitionCommand(tenantId, id, definition));
      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
    });

    // Las etiquetas del informe, todas de una vez. 404 si no existe; 400 si alguna no es una
    // etiqueta de la organización.
    group.MapPut("/{id:guid}/tags", async (IUserContext currentUser, Guid id, SetTagsRequest request, IMediator mediator) =>
    {
      var result = await mediator.Send(new Reporting.Application.Tags.SetReportTagsCommand(
          currentUser.TenantId, id, request.TagIds ?? []));

      if (result.IsSuccess) return Results.NoContent();
      return result.Error == Reporting.Application.Tags.SetReportTagsHandler.ReportNotFound
          ? Results.NotFound(result.Error)
          : Results.BadRequest(result.Error);
    });

    // Las exportaciones de un informe, con su estado y —si falló— su motivo.
    group.MapGet("/{id:guid}/exports", async (
        IUserContext currentUser, Guid id, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var result = await mediator.Send(new GetExportsQuery(tenantId, id));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/kpi", async (IUserContext currentUser, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var result = await mediator.Send(new GetKpiDataQuery(tenantId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/tasks/breakdown", async (IUserContext currentUser, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var result = await mediator.Send(new GetTaskBreakdownQuery(tenantId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/projects/progress", async (IUserContext currentUser, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var result = await mediator.Send(new GetProjectProgressQuery(tenantId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/projects/{projectId:guid}/burndown", async (IUserContext currentUser, Guid projectId, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var result = await mediator.Send(new GetProjectBurndownQuery(tenantId, projectId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    // ── Informes programados ────────────────────────────────────────────────────────────────

    group.MapGet("/{id:guid}/schedules", async (
        IUserContext currentUser, Guid id, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var result = await mediator.Send(new GetSchedulesQuery(tenantId, id));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapPost("/{id:guid}/schedules", async (
        IUserContext currentUser, Guid id, ScheduleRequest body, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var userId = currentUser.UserId;

      // El destinatario es quien programa. Programar un informe para otra persona es una decisión
      // distinta —y con implicaciones de permisos— que todavía no se ofrece.
      var result = await mediator.Send(new ScheduleReportCommand(
          tenantId, id, userId, body.Frequency, body.Format, body.Time, body.Day));

      return result.IsSuccess
          ? Results.Created($"/api/v1/reports/{id}/schedules/{result.Value!.Id}", result.Value)
          : Results.BadRequest(result.Error);
    });

    var schedules = app.MapGroup("/api/v1/schedules").WithTags("Schedules").RequireAuthorization();

    schedules.MapPatch("/{scheduleId:guid}", async (
        IUserContext currentUser, Guid scheduleId,
        ChangeScheduleRequest body, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var result = await mediator.Send(new ChangeScheduleCommand(tenantId, scheduleId, body.IsActive));
      return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
    });

    schedules.MapDelete("/{scheduleId:guid}", async (
        IUserContext currentUser, Guid scheduleId, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var result = await mediator.Send(new RemoveScheduleCommand(tenantId, scheduleId));
      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
    });

    var exports = app.MapGroup("/api/v1/exports").WithTags("Exports").RequireAuthorization();

    // El estado de una exportación. Es lo que la pantalla consulta mientras espera.
    exports.MapGet("/{exportId:guid}", async (
        IUserContext currentUser, Guid exportId, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;
      var result = await mediator.Send(new GetExportQuery(tenantId, exportId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
    });

    // La descarga.
    //
    // Es el endpoint que faltaba: antes se guardaba una URL inventada y no había nada detrás.
    // El nombre del fichero va en la respuesta para que el navegador lo use al guardarlo; sin
    // eso, el fichero se descarga con el identificador de la exportación por nombre.
    exports.MapGet("/{exportId:guid}/download", async (
        IUserContext currentUser, Guid exportId, IMediator mediator) =>
    {
      var tenantId = currentUser.TenantId;

      var result = await mediator.Send(new DownloadExportQuery(tenantId, exportId));

      // 409 y no 404: la exportación existe, lo que pasa es que todavía no está lista o falló.
      // Un 404 haría pensar que se perdió, y la pantalla dejaría de preguntar.
      return result.IsSuccess
          ? Results.File(result.Value!.Bytes, result.Value.ContentType, result.Value.Name)
          : Results.Conflict(result.Error);
    });

    app.MapDashboardEndpoints();

    return app;
  }
}

/// <summary>
/// El cuerpo de la vista previa: la definición y, opcionalmente, el título con el que enseñarla.
///
/// El título es opcional porque en el constructor el informe puede no tener nombre todavía: se
/// está probando qué enseñar antes de decidir cómo llamarlo.
/// </summary>
public sealed record PreviewRequest(
    Reporting.Domain.Definitions.ReportDefinition Definition, string? Title);

/// <summary>
/// Lo que hace falta para programar un informe.
///
/// <c>Dia</c> es el día de la semana (1 lunes … 7 domingo) o el del mes, según la frecuencia, y
/// sobra en las diarias. Un solo campo porque nunca se usan a la vez: dos harían posible guardar
/// «cada lunes día 15», que no significa nada.
/// </summary>
public sealed record ScheduleRequest(string Frequency, string Format, string Time, int? Day);

public sealed record ChangeScheduleRequest(bool IsActive);
