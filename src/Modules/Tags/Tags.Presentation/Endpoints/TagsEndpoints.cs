using BuildingBlocks.Application.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Tags.Application.Commands;
using Tags.Application.Queries;

namespace Tags.Presentation.Endpoints;

public static class TagsEndpoints
{
    public static void MapTagsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tags").WithTags("Tags").RequireAuthorization();

        // Array plano y sin paginar, como las demás listas de configuración (campos
        // personalizados, claves de entrada): una organización tiene decenas de etiquetas, no miles.
        // `language` lo manda la pantalla («en» o «es»); no se usa Accept-Language, que es el del
        // navegador y no el que la persona eligió en la aplicación.
        group.MapGet("", async ([FromQuery] string? language, IUserContext user, ISender sender) =>
        {
            var result = await sender.Send(new GetTagsQuery(user.TenantId, user.UserId, language));
            return Results.Ok(result.Value);
        })
        .WithName("GetTags")
        .WithOpenApi();

        // Los campos malformados y las categorías que no existen los rechaza el validador con un
        // 400 antes de llegar aquí; el único fallo que devuelve el handler es un nombre repetido,
        // que es un conflicto.
        group.MapPost("", async (CreateTagRequest request, IUserContext user, ISender sender) =>
        {
            var result = await sender.Send(new CreateTagCommand(
                user.TenantId, user.UserId, request.Name ?? string.Empty, request.ColorHex, request.Category ?? string.Empty));

            // Sin cabecera Location: todavía no hay un GET por id al que apuntar.
            return result.IsSuccess
                ? Results.Created((string?)null, result.Value)
                : Results.Conflict(result.Error);
        })
        .WithName("CreateTag")
        .WithOpenApi();

        // Editar y borrar: quien la creó, un administrador o alguien con el permiso «Full» sobre
        // etiquetas (ver TagAccess). 404 si no existe en la organización, 403 si no puede, 409 si es
        // de un equipo o proyecto; esos tres los traduce el manejador global de excepciones.
        group.MapPut("/{id:guid}", async (Guid id, UpdateTagRequest request, IUserContext user, ISender sender) =>
        {
            var result = await sender.Send(new UpdateTagCommand(
                user.TenantId, user.UserId, id, request.Name ?? string.Empty, request.ColorHex, request.Category ?? string.Empty));

            return result.IsSuccess ? Results.Ok(result.Value) : Results.Conflict(result.Error);
        })
        .WithName("UpdateTag")
        .WithOpenApi();

        group.MapDelete("/{id:guid}", async (Guid id, IUserContext user, ISender sender) =>
        {
            await sender.Send(new DeleteTagCommand(user.TenantId, user.UserId, id));
            return Results.NoContent();
        })
        .WithName("DeleteTag")
        .WithOpenApi();

        group.MapGet("/categories", async ([FromQuery] string? language, IUserContext user, ISender sender) =>
        {
            var result = await sender.Send(new GetTagCategoriesQuery(user.TenantId, language));
            return Results.Ok(result.Value);
        })
        .WithName("GetTagCategories")
        .WithOpenApi();

        group.MapPost("/categories", async (CreateTagCategoryRequest request, IUserContext user, ISender sender) =>
        {
            var result = await sender.Send(new CreateTagCategoryCommand(user.TenantId, request.Name ?? string.Empty));

            return result.IsSuccess
                ? Results.Created((string?)null, result.Value)
                : Results.Conflict(result.Error);
        })
        .WithName("CreateTagCategory")
        .WithOpenApi();
    }
}
