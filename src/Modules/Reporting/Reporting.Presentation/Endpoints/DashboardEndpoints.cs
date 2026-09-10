using MediatR;
using Reporting.Application.Paneles;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Reporting.Application.Dashboards.Commands;
using Reporting.Application.Dashboards.Queries;

namespace Reporting.Presentation.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/dashboards").WithTags("Dashboards").RequireAuthorization();

        // ── Mi panel ────────────────────────────────────────────────────────────────────────
        //
        // Uno por persona, no uno por inquilino: «un dashboard es de quien lo mira; si se guarda
        // por inquilino, dos personas se pisan la configuración». Se crea con los informes de
        // partida la primera vez que alguien entra.
        group.MapGet("/mio", async (System.Security.Claims.ClaimsPrincipal principal, IMediator mediator) =>
        {
            var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
            var userId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;

            var result = await mediator.Send(new GetMiPanelQuery(tenantId, userId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        // Los datos de todos los recuadros, en una sola petición. Ver GetDatosDelPanelQuery para
        // por qué no es una llamada por widget.
        group.MapGet("/{id:guid}/datos", async (
            System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator) =>
        {
            var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;

            var result = await mediator.Send(new GetDatosDelPanelQuery(tenantId, id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // PUT y no PATCH: la disposición se manda entera. Mover un recuadro cambia la posición de
        // los que lo rodean, así que enviar sólo el que se movió obligaría al servidor a recolocar
        // el resto adivinando.
        group.MapPut("/{id:guid}/disposicion", async (
            System.Security.Claims.ClaimsPrincipal principal, Guid id,
            DisposicionRequest cuerpo, IMediator mediator) =>
        {
            var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
            var userId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;

            // Con nombre y no por posición: el comando lleva tres Guid seguidos —inquilino,
            // persona y panel— y ponerlos en otro orden compila igual. Ya pasó aquí mismo: se
            // mandaba el panel donde va la persona, y la comprobación de dueño rechazaba a su
            // propio dueño.
            var result = await mediator.Send(new GuardarDisposicionCommand(
                TenantId: tenantId, UserId: userId, PanelId: id, Widgets: cuerpo.Widgets));
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        });

        group.MapPost("/{id:guid}/widgets", async (
            System.Security.Claims.ClaimsPrincipal principal, Guid id,
            AnadirWidgetRequest cuerpo, IMediator mediator) =>
        {
            var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
            var userId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;

            var result = await mediator.Send(new AnadirWidgetCommand(
                TenantId: tenantId, UserId: userId, PanelId: id, ReportId: cuerpo.ReportId, Forma: cuerpo.Forma));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        group.MapDelete("/{id:guid}/widgets/{widgetId:guid}", async (
            System.Security.Claims.ClaimsPrincipal principal, Guid id, Guid widgetId, IMediator mediator) =>
        {
            var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
            var userId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;

            var result = await mediator.Send(new QuitarWidgetCommand(
                TenantId: tenantId, UserId: userId, PanelId: id, WidgetId: widgetId));
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        });

        group.MapGet("", async (System.Security.Claims.ClaimsPrincipal principal, IMediator mediator) =>
        {
            var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
            var userId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;
            
            var query = new GetDashboardsQuery(tenantId, userId);
            var result = await mediator.Send(query);
            return Results.Ok(result);
        });

        group.MapPost("", async (System.Security.Claims.ClaimsPrincipal principal, CreateDashboardRequest request, IMediator mediator) =>
        {
            var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
            var userId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;

            var command = new CreateDashboardCommand(
                tenantId,
                request.Title,
                request.IsDefault,
                request.IsPublic,
                userId,
                request.WidgetsJson,
                request.TagIds
            );

            var result = await mediator.Send(command);
            return Results.Created($"/api/v1/dashboards/{result}", result);
        });

        group.MapPut("/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, CreateDashboardRequest request, IMediator mediator) =>
        {
            var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
            var userId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;

            var command = new UpdateDashboardCommand(
                tenantId,
                id,
                userId,
                request.Title,
                request.IsDefault,
                request.IsPublic,
                request.WidgetsJson,
                request.TagIds
            );

            var result = await mediator.Send(command);
            return result ? Results.Ok() : Results.Forbid();
        });

        group.MapDelete("/{id:guid}", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, IMediator mediator) =>
        {
            var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
            var userId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var _uid) ? _uid : Guid.Empty;

            var command = new DeleteDashboardCommand(tenantId, id, userId);
            var result = await mediator.Send(command);
            return result ? Results.Ok() : Results.Forbid();
        });

        return app;
    }
}

public record CreateDashboardRequest(
    string Title,
    bool IsDefault,
    bool IsPublic,
    string WidgetsJson,
    List<Guid> TagIds);

/// <summary>La disposición entera del panel. Ver el endpoint para por qué va entera.</summary>
public sealed record DisposicionRequest(IReadOnlyList<Reporting.Domain.Paneles.Widget> Widgets);

public sealed record AnadirWidgetRequest(Guid ReportId, string? Forma);
