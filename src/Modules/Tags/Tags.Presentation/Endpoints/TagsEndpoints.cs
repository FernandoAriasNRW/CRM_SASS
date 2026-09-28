using BuildingBlocks.Application.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
        group.MapGet("", async (IUserContext user, ISender sender) =>
        {
            var result = await sender.Send(new GetTagsQuery(user.TenantId));
            return Results.Ok(result.Value);
        })
        .WithName("GetTags")
        .WithOpenApi();

        // Los campos malformados los rechaza el validador con un 400 antes de llegar aquí; el único
        // fallo que devuelve el handler es un nombre repetido, que es un conflicto.
        group.MapPost("", async (CreateTagRequest request, IUserContext user, ISender sender) =>
        {
            var result = await sender.Send(new CreateTagCommand(
                user.TenantId, request.Name ?? string.Empty, request.ColorHex, request.Category));

            // Sin cabecera Location: todavía no hay un GET por id al que apuntar.
            return result.IsSuccess
                ? Results.Created((string?)null, result.Value)
                : Results.Conflict(result.Error);
        })
        .WithName("CreateTag")
        .WithOpenApi();
    }
}
