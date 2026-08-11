using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using System.Security.Claims;

namespace Tags.Presentation.Endpoints;

public static class TagsEndpoints
{
    public static void MapTagsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tags").RequireAuthorization();

        // Get all tags
        group.MapGet("/", async (ISender sender) =>
        {
            // var query = new GetTagsQuery();
            // var result = await sender.Send(query);
            // return Results.Ok(result.Value);
            return Results.Ok(new object[] {}); // placeholder
        })
        .WithName("GetTags")
        .WithOpenApi();
        
        // Create Tag
        group.MapPost("/", async (ISender sender, HttpContext context) =>
        {
            return Results.Ok();
        })
        .WithName("CreateTag")
        .WithOpenApi();
    }
}
