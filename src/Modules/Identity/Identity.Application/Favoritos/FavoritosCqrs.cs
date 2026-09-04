using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Domain.Entities;

namespace Identity.Application.Favoritos;

public interface IRepositorioDeFavoritos
{
    /// <summary>Los identificadores que esta persona marcó de un tipo. Es lo que usa el filtro.</summary>
    Task<IReadOnlyList<Guid>> IdsDeAsync(Guid tenantId, Guid userId, string tipo, CancellationToken ct);

    Task<Favorito?> BuscarAsync(Guid tenantId, Guid userId, string tipo, Guid entityId, CancellationToken ct);
    Task<int> CuantosAsync(Guid tenantId, Guid userId, string tipo, CancellationToken ct);

    Task AnadirAsync(Favorito favorito, CancellationToken ct);
    void Quitar(Favorito favorito);
    Task GuardarAsync(CancellationToken ct);
}

public sealed record GetFavoritosQuery(Guid TenantId, Guid UserId, string Tipo)
    : IQuery<IReadOnlyList<Guid>>;

/// <summary>
/// Pone o quita la estrella, según cómo esté.
///
/// **Es una sola operación y no dos.** La estrella es un interruptor: quien la pulsa quiere que
/// cambie, no «marcar» o «desmarcar» según lo que crea que hay. Con dos endpoints separados, dos
/// pestañas abiertas acaban peleándose —una marca lo que la otra acaba de desmarcar— y desde el
/// cliente habría que saber el estado actual para elegir a cuál llamar.
/// </summary>
public sealed record AlternarFavoritoCommand(Guid TenantId, Guid UserId, string Tipo, Guid EntityId)
    : ICommand<bool>;

public sealed class GetFavoritosHandler(IRepositorioDeFavoritos repositorio)
    : IQueryHandler<GetFavoritosQuery, IReadOnlyList<Guid>>
{
    public async Task<Result<IReadOnlyList<Guid>>> Handle(GetFavoritosQuery peticion, CancellationToken ct)
    {
        if (!TipoDeFavorito.Existe(peticion.Tipo))
            return Result<IReadOnlyList<Guid>>.Failure(Favorito.Reglas.TipoDesconocido);

        return Result<IReadOnlyList<Guid>>.Success(
            await repositorio.IdsDeAsync(peticion.TenantId, peticion.UserId, peticion.Tipo, ct));
    }
}

/// <summary>Devuelve cómo quedó: <c>true</c> si ahora está marcado.</summary>
public sealed class AlternarFavoritoHandler(IRepositorioDeFavoritos repositorio)
    : ICommandHandler<AlternarFavoritoCommand, bool>
{
    public async Task<Result<bool>> Handle(AlternarFavoritoCommand peticion, CancellationToken ct)
    {
        if (!TipoDeFavorito.Existe(peticion.Tipo))
            return Result<bool>.Failure(Favorito.Reglas.TipoDesconocido);

        var existente = await repositorio.BuscarAsync(
            peticion.TenantId, peticion.UserId, peticion.Tipo, peticion.EntityId, ct);

        if (existente is not null)
        {
            repositorio.Quitar(existente);
            await repositorio.GuardarAsync(ct);
            return Result<bool>.Success(false);
        }

        // El tope se comprueba antes de insertar. Sin él, el filtro por favoritos acabaría
        // construyendo una consulta con miles de identificadores dentro.
        var cuantos = await repositorio.CuantosAsync(peticion.TenantId, peticion.UserId, peticion.Tipo, ct);

        if (cuantos >= Favorito.MaximoPorPersonaYTipo)
            return Result<bool>.Failure(Favorito.Reglas.Demasiados);

        Favorito nuevo;
        try
        {
            nuevo = Favorito.Marcar(peticion.TenantId, peticion.UserId, peticion.Tipo, peticion.EntityId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.Failure(ex.Message);
        }

        await repositorio.AnadirAsync(nuevo, ct);
        await repositorio.GuardarAsync(ct);

        return Result<bool>.Success(true);
    }
}
