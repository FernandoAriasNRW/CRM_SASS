using BuildingBlocks.Application.Abstractions;
using Docs.Application.Mentions;
using Docs.Domain.Mentions;
using Microsoft.EntityFrameworkCore;

namespace Docs.Infrastructure.Persistence;

/// <summary>
/// Responde el puerto <see cref="IDocumentMentions"/> para el inquilino de la petición.
///
/// Lo implementa Docs porque es quien guarda las menciones, y lo consumen las pantallas de tareas
/// y tickets sin conocerlo: la dependencia va de todos a BuildingBlocks, nunca entre módulos. Es
/// el mismo reparto que con los favoritos y la visibilidad.
/// </summary>
public sealed class DocumentMentionsAdapter(
    IMentionRepository repository,
    IUserContext userContext) : IDocumentMentions
{
    public Task<IReadOnlyList<MentioningDocument>> GetMentioningDocumentsAsync(
        string type, Guid entityId, CancellationToken ct = default)
        => repository.GetMentioningDocumentsAsync(userContext.TenantId, type, entityId, ct);
}
