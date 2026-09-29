using BuildingBlocks.Application.Abstractions;
using Tags.Application.DTOs;

namespace Tags.Application.Queries;

/// <param name="Language">«en» para inglés; cualquier otro valor o ninguno, español.</param>
public sealed record GetTagCategoriesQuery(Guid TenantId, string? Language) : IQuery<List<TagCategoryDto>>;
