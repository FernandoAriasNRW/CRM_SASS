using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Application.Abstractions;
using Identity.Application.Abstractions.Repositories;
using Identity.Domain.Entities;
using Identity.Domain.Permissions;

namespace Identity.Application.Sharing;

/// <summary>
/// Compartir con alguien, o cambiarle el nivel si ya lo tenía.
///
/// Se apoya en la tabla de permisos por entidad que ya existía (<see cref="EntityPermission"/>)
/// en vez de crear una tabla de compartición nueva. Dos tablas diciendo quién ve qué acabarían
/// discrepando, y entonces «¿quién ve esto?» tendría dos respuestas y ninguna fiable.
///
/// <b>El nombre del tipo se traduce en un solo sitio</b>, <see cref="PermissionTypes.FromEntityType"/>:
/// repartida por los módulos sería la misma cadena escrita a mano en ocho ficheros.
/// </summary>
public sealed record ShareCommand(
    Guid TenantId,
    string EntityType,
    Guid EntityId,
    Guid WithUserId,
    string Level) : ICommand<bool>;
