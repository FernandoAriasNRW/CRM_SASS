using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using Projects.Domain.Entities;

namespace Projects.Application.Commands;

/// <summary>
/// Los espacios y las carpetas ordenan los proyectos, así que tocarlos pide lo mismo que crear un
/// proyecto: poder editar proyectos. Antes los creaba y renombraba cualquiera de la organización,
/// aunque su rol solo le dejara ver.
/// </summary>
public abstract record ProjectStructureCommand : IAuthorizeEntity
{
    public string EntityType => "Project";
    public Guid EntityId => Guid.Empty;
    public string RequiredPermission => "Write";
}

public sealed record CreateSpaceCommand(Guid TenantId, string Name, string Description, string Color) : ProjectStructureCommand, ICommand<Space>;
public sealed record UpdateSpaceCommand(Guid TenantId, Guid SpaceId, string Name, string Description, string Color) : ProjectStructureCommand, ICommand<bool>;
public sealed record DeleteSpaceCommand(Guid TenantId, Guid SpaceId, Guid DeletedBy) : ICommand<bool>;
