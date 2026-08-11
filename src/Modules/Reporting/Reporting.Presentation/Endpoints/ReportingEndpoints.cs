using System.Linq;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Commands;
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

    group.MapPost("/{id:guid}/generate", async (System.Security.Claims.ClaimsPrincipal principal, Guid id, string format, IMediator mediator) =>
    {
      var tenantId = Guid.TryParse(principal.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value, out var _tid) ? _tid : Guid.Empty;
      var dto = await mediator.Send(new GetReportByIdQuery(tenantId, id));
      if (dto.Value == null) return Results.NotFound();

      var command = new GenerateReportCommand(tenantId, id, dto.Value.Type, format);
      var result = await mediator.Send(command);
      return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
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

    app.MapDashboardEndpoints();

    return app;
  }
}
