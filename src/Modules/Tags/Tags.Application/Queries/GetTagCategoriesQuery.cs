using BuildingBlocks.Application.Abstractions;
using Tags.Application.DTOs;

namespace Tags.Application.Queries;

/// <param name="UserId">Quien pregunta, para decirle si puede renombrar y borrar las propias.</param>
/// <param name="Language">«en» para inglés; cualquier otro valor o ninguno, español.</param>
public sealed record GetTagCategoriesQuery(Guid TenantId, Guid UserId, string? Language) : IQuery<List<TagCategoryDto>>;
