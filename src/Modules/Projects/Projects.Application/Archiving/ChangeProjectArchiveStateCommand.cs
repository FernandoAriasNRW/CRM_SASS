using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Projects.Application.Abstractions;
using Projects.Application.Abstractions.Repositories;

namespace Projects.Application;

/// <summary>
/// Archivar y desarchivar un proyecto, y sacarlo de la papelera.
///
/// La papelera del proyecto ya existía —<c>Delete</c> y <c>Restore</c> en el agregado, con su
/// evento de dominio— así que aquí no se duplica: enviar a la papelera sigue siendo
/// <c>DeleteProjectCommand</c>. Lo que faltaba era el archivo, y una forma de restaurar desde
/// la pantalla de la papelera.
/// </summary>
public sealed record ChangeProjectArchiveStateCommand(
    Guid TenantId,
    Guid Id,
    Guid ActorId,
    ArchiveAction Action) : ICommand<bool>, IAuthorizeEntity
{
    public string EntityType => "Project";
    public Guid EntityId => Id;
    public string RequiredPermission => "Write";
}
