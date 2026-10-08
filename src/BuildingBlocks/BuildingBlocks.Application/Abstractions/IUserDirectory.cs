namespace BuildingBlocks.Application.Abstractions;

/// <summary>
/// Quién es quién en una organización, para los módulos que necesitan saberlo sin ser Identity.
///
/// Lo usa sobre todo el envío de avisos: un aviso sólo puede ir a alguien que existe en la
/// organización —un comentario puede mencionar un identificador inventado—, y los avisos de
/// administración sólo a quien administra. Lo implementa Identity.
/// </summary>
public interface IUserDirectory
{
    /// <summary>
    /// Las personas de la lista que existen en la organización y no están borradas. Las que no, no
    /// salen: así se descartan los identificadores inventados o de otra organización.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, DirectoryUser>> GetAsync(Guid tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);

    /// <summary>Quienes administran la organización.</summary>
    Task<IReadOnlyList<Guid>> GetAdminIdsAsync(Guid tenantId, CancellationToken ct = default);
}

/// <summary>Lo justo de una persona para decidir si le llega un aviso y cómo nombrarla.</summary>
public sealed record DirectoryUser(Guid Id, string Name, bool IsAdmin);
