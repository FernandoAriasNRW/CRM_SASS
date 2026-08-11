using BuildingBlocks.Domain.Primitives;

namespace Docs.Domain.Entities;

public sealed class DocumentPermission : Entity
{
    public Guid DocumentId { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid? TeamId { get; private set; }
    public bool CanRead { get; private set; }
    public bool CanWrite { get; private set; }
    public bool CanDownload { get; private set; }

    private DocumentPermission() { }

    public static DocumentPermission CreateForUser(Guid documentId, Guid userId, bool canRead, bool canWrite, bool canDownload)
    {
        return new DocumentPermission
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            UserId = userId,
            CanRead = canRead,
            CanWrite = canWrite,
            CanDownload = canDownload
        };
    }

    public static DocumentPermission CreateForTeam(Guid documentId, Guid teamId, bool canRead, bool canWrite, bool canDownload)
    {
        return new DocumentPermission
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            TeamId = teamId,
            CanRead = canRead,
            CanWrite = canWrite,
            CanDownload = canDownload
        };
    }

    public static DocumentPermission CreatePublic(Guid documentId, bool canRead, bool canWrite, bool canDownload)
    {
        return new DocumentPermission
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            UserId = null,
            TeamId = null,
            CanRead = canRead,
            CanWrite = canWrite,
            CanDownload = canDownload
        };
    }
}
