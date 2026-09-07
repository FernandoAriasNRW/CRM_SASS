using System.Linq;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Commands;
using Reporting.Application.Definiciones;
using Reporting.Application.Exportaciones;
using Reporting.Application.Programaciones;
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

    group.MapGet("", async (System.Security.Claims.ClaimsPrincipal principal, string? type, IMediator mediator, int page = 1, int pageSize = 25) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var query = new GetReportsQuery(tenantId, type, new() { Page = page, PageSize = pageSize });
      var result = await mediator.Send(query);
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var result = await mediator.Send(new GetReportByIdQuery(tenantId, id));
      return result.Value is null ? Results.NotFound() : Results.Ok(result.Value);
    });

    group.MapPost("", async (System.Security.Claims.ClaimsPrincipal principal, CreateReportCommand command, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var userId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;
      
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
    group.MapPost("/{id:guid}/exportar", async (
        System.Security.Claims.ClaimsPrincipal principal, Guid id, string format, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var userId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;

      var result = await mediator.Send(new SolicitarExportacionCommand(tenantId, id, userId, format));

      return result.IsSuccess
          ? Results.Accepted($"/api/v1/exportaciones/{result.Value!.Id}", result.Value)
          : Results.BadRequest(result.Error);
    });

    // La ruta antigua, que se mantiene para no romper la pantalla mientras se actualiza.
    //
    // Hace **lo mismo** que la nueva —encolar una exportación de verdad— en vez de lo que hacía
    // antes. Se conserva el nombre, no el comportamiento: fingir que se generó algo era el fallo.
    group.MapPost("/{id:guid}/generate", async (
        System.Security.Claims.ClaimsPrincipal principal, Guid id, string format, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var userId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;

      var result = await mediator.Send(new SolicitarExportacionCommand(tenantId, id, userId, format));

      return result.IsSuccess
          ? Results.Accepted($"/api/v1/exportaciones/{result.Value!.Id}", result.Value)
          : Results.BadRequest(result.Error);
    });

    // ── El constructor de informes ──────────────────────────────────────────────────────────
    //
    // El catálogo va primero y sin autenticar por inquilino porque no depende de ninguno: es la
    // lista de lo que el motor sabe hacer. La pantalla se alimenta de aquí y no escribe ninguna
    // de estas opciones por su cuenta, que es lo que evita volver a ofrecer algo que el servidor
    // no conoce.
    group.MapGet("/catalogo", async (IMediator mediator) =>
    {
      var result = await mediator.Send(new GetCatalogoQuery());
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    // La vista previa: enseña el resultado **antes** de guardar. Sin ella, construir un informe
    // es escribir a ciegas y descubrir el resultado al exportarlo.
    group.MapPost("/vista-previa", async (
        System.Security.Claims.ClaimsPrincipal principal, VistaPreviaRequest cuerpo, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;

      var result = await mediator.Send(new VistaPreviaQuery(
          tenantId, cuerpo.Titulo ?? "Vista previa", cuerpo.Definicion));

      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/{id:guid}/definicion", async (
        System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var result = await mediator.Send(new GetDefinicionQuery(tenantId, id));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
    });

    // PUT y no POST: guardar la definición dos veces deja el mismo informe. Con POST, la segunda
    // llamada tendría que decidir si es un conflicto, y no lo es.
    group.MapPut("/{id:guid}/definicion", async (
        System.Security.Claims.ClaimsPrincipal principal, Guid id,
        Reporting.Domain.Definicion.DefinicionDeInforme definicion, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;

      var result = await mediator.Send(new GuardarDefinicionCommand(tenantId, id, definicion));
      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
    });

    // Las exportaciones de un informe, con su estado y —si falló— su motivo.
    group.MapGet("/{id:guid}/exportaciones", async (
        System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var result = await mediator.Send(new GetExportacionesQuery(tenantId, id));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/kpi", async (System.Security.Claims.ClaimsPrincipal principal, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var result = await mediator.Send(new GetKpiDataQuery(tenantId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/tasks/breakdown", async (System.Security.Claims.ClaimsPrincipal principal, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var result = await mediator.Send(new GetTaskBreakdownQuery(tenantId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/projects/progress", async (System.Security.Claims.ClaimsPrincipal principal, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var result = await mediator.Send(new GetProjectProgressQuery(tenantId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapGet("/projects/{projectId:guid}/burndown", async (System.Security.Claims.ClaimsPrincipal principal, Guid projectId, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var result = await mediator.Send(new GetProjectBurndownQuery(tenantId, projectId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    // ── Informes programados ────────────────────────────────────────────────────────────────

    group.MapGet("/{id:guid}/programaciones", async (
        System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var result = await mediator.Send(new GetProgramacionesQuery(tenantId, id));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
    });

    group.MapPost("/{id:guid}/programaciones", async (
        System.Security.Claims.ClaimsPrincipal principal, Guid id, ProgramarRequest cuerpo, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var userId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;

      // El destinatario es quien programa. Programar un informe para otra persona es una decisión
      // distinta —y con implicaciones de permisos— que todavía no se ofrece.
      var result = await mediator.Send(new ProgramarInformeCommand(
          tenantId, id, userId, cuerpo.Frecuencia, cuerpo.Formato, cuerpo.Hora, cuerpo.Dia));

      return result.IsSuccess
          ? Results.Created($"/api/v1/reports/{id}/programaciones/{result.Value!.Id}", result.Value)
          : Results.BadRequest(result.Error);
    });

    var programaciones = app.MapGroup("/api/v1/programaciones").WithTags("Programaciones").RequireAuthorization();

    programaciones.MapPatch("/{programacionId:guid}", async (
        System.Security.Claims.ClaimsPrincipal principal, Guid programacionId,
        CambiarProgramacionRequest cuerpo, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var result = await mediator.Send(new CambiarProgramacionCommand(tenantId, programacionId, cuerpo.Activa));
      return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
    });

    programaciones.MapDelete("/{programacionId:guid}", async (
        System.Security.Claims.ClaimsPrincipal principal, Guid programacionId, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var result = await mediator.Send(new QuitarProgramacionCommand(tenantId, programacionId));
      return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
    });

    var exportaciones = app.MapGroup("/api/v1/exportaciones").WithTags("Exportaciones").RequireAuthorization();

    // El estado de una exportación. Es lo que la pantalla consulta mientras espera.
    exportaciones.MapGet("/{exportacionId:guid}", async (
        System.Security.Claims.ClaimsPrincipal principal, Guid exportacionId, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var result = await mediator.Send(new GetExportacionQuery(tenantId, exportacionId));
      return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
    });

    // La descarga.
    //
    // Es el endpoint que faltaba: antes se guardaba una URL inventada y no había nada detrás.
    // El nombre del fichero va en la respuesta para que el navegador lo use al guardarlo; sin
    // eso, el fichero se descarga con el identificador de la exportación por nombre.
    exportaciones.MapGet("/{exportacionId:guid}/descargar", async (
        System.Security.Claims.ClaimsPrincipal principal, Guid exportacionId, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;

      var result = await mediator.Send(new DescargarExportacionQuery(tenantId, exportacionId));

      // 409 y no 404: la exportación existe, lo que pasa es que todavía no está lista o falló.
      // Un 404 haría pensar que se perdió, y la pantalla dejaría de preguntar.
      return result.IsSuccess
          ? Results.File(result.Value!.Bytes, result.Value.TipoDeContenido, result.Value.Nombre)
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
public sealed record VistaPreviaRequest(
    Reporting.Domain.Definicion.DefinicionDeInforme Definicion, string? Titulo);

/// <summary>
/// Lo que hace falta para programar un informe.
///
/// <c>Dia</c> es el día de la semana (1 lunes … 7 domingo) o el del mes, según la frecuencia, y
/// sobra en las diarias. Un solo campo porque nunca se usan a la vez: dos harían posible guardar
/// «cada lunes día 15», que no significa nada.
/// </summary>
public sealed record ProgramarRequest(string Frecuencia, string Formato, string Hora, int? Dia);

public sealed record CambiarProgramacionRequest(bool Activa);
