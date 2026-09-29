using BuildingBlocks.Application.Abstractions;
using Tags.Application.DTOs;

namespace Tags.Application.Queries;

/// <param name="UserId">Quien pregunta, para decirle en cada etiqueta si puede gestionarla.</param>
/// <param name="Language">«en» para inglés; cualquier otro valor o ninguno, español.</param>
public sealed record GetTagsQuery(Guid TenantId, Guid UserId, string? Language) : IQuery<List<TagDto>>;
