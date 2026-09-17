using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Domain.Entities;

namespace Identity.Application.Favorites;

/// <summary>
/// Pone o quita la estrella, según cómo esté.
///
/// **Es una sola operación y no dos.** La estrella es un interruptor: quien la pulsa quiere que
/// cambie, no «marcar» o «desmarcar» según lo que crea que hay. Con dos endpoints separados, dos
/// pestañas abiertas acaban peleándose —una marca lo que la otra acaba de desmarcar— y desde el
/// cliente habría que saber el estado actual para elegir a cuál llamar.
/// </summary>
public sealed record ToggleFavoriteCommand(Guid TenantId, Guid UserId, string EntityType, Guid EntityId)
    : ICommand<bool>;
