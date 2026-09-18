using BuildingBlocks.Application.Abstractions;
using Docs.Application.Commands;
using Docs.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
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

        group.MapPost("/", async ([FromBody] CreateDocumentRequest req, IUserContext currentUser, IMediator mediator) =>
        {
            if (currentUser.TenantId == Guid.Empty || currentUser.UserId == Guid.Empty)
                return Results.Unauthorized();

            var command = new CreateDocumentCommand(
                currentUser.TenantId,
                currentUser.UserId,
                req.Title,
                req.Description,
                req.Type,
                req.TeamId,
                req.ProjectId,
                req.InitialContent);
                
            var result = await mediator.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        group.MapGet("/", async (IUserContext currentUser, IMediator mediator) =>
        {
            if (currentUser.TenantId == Guid.Empty || currentUser.UserId == Guid.Empty)
                return Results.Unauthorized();

            var query = new GetDocumentsQuery(currentUser.TenantId, currentUser.UserId);
            var result = await mediator.Send(query);
            
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var command = new DeleteDocumentCommand(id);
            var result = await mediator.Send(command);
            return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
        });

        group.MapDelete("/pages/{pageId:guid}", async (Guid pageId, IMediator mediator) =>
        {
            var command = new DeletePageCommand(pageId);
            var result = await mediator.Send(command);
            return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
        });

        group.MapPost("/{id:guid}/save-as-template", async (Guid id, [FromBody] SaveAsTemplateRequest req, IUserContext currentUser, IMediator mediator) =>
        {
            if (currentUser.TenantId == Guid.Empty || currentUser.UserId == Guid.Empty) return Results.Unauthorized();

            var command = new SaveAsTemplateCommand(
                currentUser.TenantId,
                currentUser.UserId,
                id,
                req.CustomTitle,
                req.Description);

            var result = await mediator.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        // Cuánto se usa cada plantilla, para que la galería enseñe cuatro que valgan la pena.
        group.MapGet("/templates/usage", async (IUserContext currentUser, IMediator mediator) =>
        {
            if (currentUser.TenantId == Guid.Empty) return Results.Unauthorized();

            var result = await mediator.Send(new GetTemplateUsagesQuery(currentUser.TenantId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        group.MapPost("/from-template", async ([FromBody] CreateFromTemplateRequest req, IUserContext currentUser, IMediator mediator) =>
        {
            if (currentUser.TenantId == Guid.Empty || currentUser.UserId == Guid.Empty) return Results.Unauthorized();

            var command = new CreateFromTemplateCommand(
                currentUser.TenantId,
                currentUser.UserId,
                req.TemplateKey,
                req.TemplateDocumentId,
                req.CustomTitle,
                req.Language);

            var result = await mediator.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        group.MapPost("/import", async ([FromBody] ImportDocumentRequest req, IUserContext currentUser, IMediator mediator) =>
        {
            if (currentUser.TenantId == Guid.Empty || currentUser.UserId == Guid.Empty) return Results.Unauthorized();

            var command = new ImportDocumentCommand(
                currentUser.TenantId,
                currentUser.UserId,
                req.Title,
                req.Content,
                req.Type);

            var result = await mediator.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        group.MapPost("/upload", async (IFormFile file, IUserContext currentUser, IMediator mediator) =>
        {
            if (currentUser.TenantId == Guid.Empty) return Results.Unauthorized();

            using var stream = file.OpenReadStream();
            var command = new Docs.Application.Handlers.Commands.UploadFileCommand(stream, file.FileName, file.ContentType);
            var result = await mediator.Send(command);
            
            return result.IsSuccess ? Results.Ok(new { url = result.Value }) : Results.BadRequest(result.Error);
        }).DisableAntiforgery();

        group.MapPost("/{id:guid}/pages", async (Guid id, [FromBody] CreatePageRequest req, IMediator mediator) =>
        {
            var command = new Docs.Application.Handlers.Commands.CreatePageCommand(id, req.ParentPageId, req.Title);
            var result = await mediator.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        // Renombrar un documento. No había forma de hacerlo: el campo de título de la pantalla
        // escribía en la página activa porque no existía este endpoint.
        group.MapPut("/{id:guid}", async (Guid id, [FromBody] RenameDocumentRequest req, IMediator mediator) =>
        {
            var command = new Docs.Application.Handlers.Commands.RenameDocumentCommand(
                id, req.Title, req.Description);

            var result = await mediator.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        });

        // Mover una página dentro del árbol del documento: de padre, de orden, o las dos.
        group.MapPut("/pages/{pageId:guid}/move", async (Guid pageId, [FromBody] MovePageRequest req, IMediator mediator) =>
        {
            var command = new Docs.Application.Handlers.Commands.MovePageCommand(
                pageId, req.ParentPageId, req.Order);

            var result = await mediator.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        });

        group.MapPut("/pages/{pageId:guid}", async (Guid pageId, [FromBody] UpdatePageRequest req, IMediator mediator) =>
        {
            var command = new Docs.Application.Handlers.Commands.UpdatePageCommand(pageId, req.Title, req.Content);
            var result = await mediator.Send(command);
            return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
        });

        // ── Comentarios en línea ────────────────────────────────────────────────────────────
        //
        // Docs guarda **dónde** está pegado el comentario; el hilo lo guarda el módulo Comments,
        // con el identificador de la anotación como entidad comentada. Son dos cosas distintas y
        // se piden por separado: juntarlas aquí obligaría a Docs a conocer a Comments.

        group.MapGet("/pages/{pageId:guid}/annotations", async (Guid pageId, IMediator mediator) =>
        {
            var result = await mediator.Send(new Docs.Application.Annotations.GetPageAnnotationsQuery(pageId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        group.MapPost("/pages/{pageId:guid}/annotations", async (
            Guid pageId, [FromBody] CreateAnnotationRequest req, IUserContext currentUser, IMediator mediator) =>
        {
            if (currentUser.TenantId == Guid.Empty || currentUser.UserId == Guid.Empty) return Results.Unauthorized();

            var command = new Docs.Application.Annotations.CreateAnnotationCommand(
                currentUser.TenantId, Guid.Empty, pageId, currentUser.UserId, req.QuotedText);

            var result = await mediator.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        group.MapPut("/annotations/{id:guid}/resolve", async (
            Guid id, [FromBody] ResolveAnnotationRequest req, IUserContext currentUser, IMediator mediator) =>
        {
            if (currentUser.UserId == Guid.Empty) return Results.Unauthorized();

            var command = new Docs.Application.Annotations.ResolveAnnotationCommand(
                id, currentUser.UserId, req.IsResolved);

            var result = await mediator.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        });

        group.MapDelete("/annotations/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new Docs.Application.Annotations.DeleteAnnotationCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        });

        // ── Menciones ───────────────────────────────────────────────────────────────────────
        //
        // «¿Qué documentos hablan de esta tarea?». Va bajo /docs y no bajo la tarea porque la
        // respuesta es una lista de documentos y la da Docs; la pantalla de la tarea la consume
        // sin que WorkItems tenga que conocer a Docs.
        group.MapGet("/mentions/{type}/{entityId:guid}", async (
            string type, Guid entityId,
            BuildingBlocks.Application.Abstractions.IDocumentMentions mentions) =>
        {
            if (!Docs.Domain.Mentions.MentionableTypes.Exists(type))
            {
                return Results.BadRequest(
                    $"«{type}» no se puede mencionar. Los que sí: "
                    + string.Join(", ", Docs.Domain.Mentions.MentionableTypes.All()));
            }

            return Results.Ok(await mentions.GetMentioningDocumentsAsync(type, entityId));
        });

        group.MapGet("/{id:guid}/pages", async (Guid id, IMediator mediator) =>
        {
            var query = new GetPagesQuery(id);
            var result = await mediator.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        group.MapGet("/{id:guid}/export", async (Guid id, IMediator mediator) =>
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

/// <summary>La descripción es opcional: renombrar desde el título no debe borrarla.</summary>
public record RenameDocumentRequest(string Title, string? Description);

public record MovePageRequest(Guid? ParentPageId, int Order);

/// <summary>El documento no viaja: se saca de la página, para que no pueda venir mal desde fuera.</summary>
public record CreateAnnotationRequest(string QuotedText);

public record ResolveAnnotationRequest(bool IsResolved);
public record SaveAsTemplateRequest(string? CustomTitle, string? Description);
public record CreateFromTemplateRequest(string? TemplateKey, Guid? TemplateDocumentId, string? CustomTitle, string? Language = null);
public record ImportDocumentRequest(string Title, string Content, int Type = 1);
