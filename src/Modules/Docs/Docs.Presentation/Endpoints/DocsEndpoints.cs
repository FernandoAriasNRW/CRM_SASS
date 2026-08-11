using Docs.Application.Commands;
using Docs.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Docs.Presentation.Endpoints;

public static class DocsEndpointsExtensions
{
    public static IServiceCollection AddDocsPresentation(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }

    public static IEndpointRouteBuilder MapDocsEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/api/v1/docs").RequireAuthorization();

        group.MapPost("/", async ([FromBody] CreateDocumentRequest req, HttpContext context, IMediator mediator) =>
        {
            var tenantIdStr = context.User.FindFirst("tenantId")?.Value;
            var userIdStr = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(tenantIdStr) || string.IsNullOrEmpty(userIdStr))
                return Results.Unauthorized();

            var command = new CreateDocumentCommand(
                Guid.Parse(tenantIdStr),
                Guid.Parse(userIdStr),
                req.Title,
                req.Description,
                req.Type,
                req.TeamId,
                req.ProjectId,
                req.InitialContent);
                
            var result = await mediator.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        group.MapGet("/", async (HttpContext context, IMediator mediator) =>
        {
            var tenantIdStr = context.User.FindFirst("tenantId")?.Value;
            var userIdStr = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(tenantIdStr) || string.IsNullOrEmpty(userIdStr))
                return Results.Unauthorized();

            var query = new GetDocumentsQuery(Guid.Parse(tenantIdStr), Guid.Parse(userIdStr));
            var result = await mediator.Send(query);
            
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext context, IMediator mediator) =>
        {
            var command = new DeleteDocumentCommand(id);
            var result = await mediator.Send(command);
            return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
        });

        group.MapDelete("/pages/{pageId:guid}", async (Guid pageId, HttpContext context, IMediator mediator) =>
        {
            var command = new DeletePageCommand(pageId);
            var result = await mediator.Send(command);
            return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
        });

        group.MapPost("/{id:guid}/save-as-template", async (Guid id, [FromBody] SaveAsTemplateRequest req, HttpContext context, IMediator mediator) =>
        {
            var tenantIdStr = context.User.FindFirst("tenantId")?.Value;
            var userIdStr = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(tenantIdStr) || string.IsNullOrEmpty(userIdStr)) return Results.Unauthorized();

            var command = new SaveAsTemplateCommand(
                Guid.Parse(tenantIdStr),
                Guid.Parse(userIdStr),
                id,
                req.CustomTitle,
                req.Description);

            var result = await mediator.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        group.MapPost("/from-template", async ([FromBody] CreateFromTemplateRequest req, HttpContext context, IMediator mediator) =>
        {
            var tenantIdStr = context.User.FindFirst("tenantId")?.Value;
            var userIdStr = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(tenantIdStr) || string.IsNullOrEmpty(userIdStr)) return Results.Unauthorized();

            var command = new CreateFromTemplateCommand(
                Guid.Parse(tenantIdStr),
                Guid.Parse(userIdStr),
                req.TemplateKey,
                req.TemplateDocumentId,
                req.CustomTitle);

            var result = await mediator.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        group.MapPost("/import", async ([FromBody] ImportDocumentRequest req, HttpContext context, IMediator mediator) =>
        {
            var tenantIdStr = context.User.FindFirst("tenantId")?.Value;
            var userIdStr = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(tenantIdStr) || string.IsNullOrEmpty(userIdStr)) return Results.Unauthorized();

            var command = new ImportDocumentCommand(
                Guid.Parse(tenantIdStr),
                Guid.Parse(userIdStr),
                req.Title,
                req.Content,
                req.Type);

            var result = await mediator.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        group.MapPost("/upload", async (IFormFile file, HttpContext context, IMediator mediator) =>
        {
            var tenantIdStr = context.User.FindFirst("tenantId")?.Value;
            if (string.IsNullOrEmpty(tenantIdStr)) return Results.Unauthorized();

            using var stream = file.OpenReadStream();
            var command = new Docs.Application.Handlers.Commands.UploadFileCommand(stream, file.FileName, file.ContentType);
            var result = await mediator.Send(command);
            
            return result.IsSuccess ? Results.Ok(new { url = result.Value }) : Results.BadRequest(result.Error);
        }).DisableAntiforgery();

        group.MapPost("/{id:guid}/pages", async (Guid id, [FromBody] CreatePageRequest req, HttpContext context, IMediator mediator) =>
        {
            var command = new Docs.Application.Handlers.Commands.CreatePageCommand(id, req.ParentPageId, req.Title);
            var result = await mediator.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        group.MapPut("/pages/{pageId:guid}", async (Guid pageId, [FromBody] UpdatePageRequest req, HttpContext context, IMediator mediator) =>
        {
            var command = new Docs.Application.Handlers.Commands.UpdatePageCommand(pageId, req.Title, req.Content);
            var result = await mediator.Send(command);
            return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
        });

        group.MapGet("/{id:guid}/pages", async (Guid id, HttpContext context, IMediator mediator) =>
        {
            var query = new GetPagesQuery(id);
            var result = await mediator.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        group.MapGet("/{id:guid}/export", async (Guid id, HttpContext context, IMediator mediator) =>
        {
            var query = new Docs.Application.Queries.ExportDocumentQuery(id);
            var result = await mediator.Send(query);
            
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            
            return Results.File(System.Text.Encoding.UTF8.GetBytes(result.Value ?? ""), "text/html", $"document_{id}.html");
        });

        return builder;
    }
}

public record CreateDocumentRequest(string Title, string Description, int Type, Guid? TeamId, Guid? ProjectId, string? InitialContent = null);
public record CreatePageRequest(Guid? ParentPageId, string Title);
public record UpdatePageRequest(string Title, string Content);
public record SaveAsTemplateRequest(string? CustomTitle, string? Description);
public record CreateFromTemplateRequest(string? TemplateKey, Guid? TemplateDocumentId, string? CustomTitle);
public record ImportDocumentRequest(string Title, string Content, int Type = 1);
