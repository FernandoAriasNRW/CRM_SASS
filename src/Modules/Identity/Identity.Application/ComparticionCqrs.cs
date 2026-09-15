using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Application.Abstractions;
using Identity.Application.Abstractions.Repositories;
using Identity.Domain.Entities;

namespace Identity.Application.Comparticion;

/// <summary>
/// Compartir algo con una persona concreta, y dejar de compartirlo.
///
/// Se apoya en la tabla de permisos por entidad que ya existía (<see cref="EntityPermission"/>)
/// en vez de crear una tabla de compartición nueva. Dos tablas diciendo quién ve qué acabarían
/// discrepando, y entonces «¿quién ve esto?» tendría dos respuestas y ninguna fiable.
///
/// <b>El nombre del tipo se traduce aquí.</b> Los módulos hablan en
/// <see cref="TiposDeEntidad"/> («Tarea»); la tabla de permisos usa el vocabulario con el que
/// se consulta al autorizar («Task»). La traducción vive en un solo sitio, que es
/// <see cref="ComoLoLlamaElPermiso"/>: repartida por los módulos sería la misma cadena escrita a
/// mano en ocho ficheros.
/// </summary>
public static class VocabularioDePermisos
{
    /// <summary>
    /// Cómo llama la tabla de permisos a cada tipo de entidad.
    ///
    /// Se usa el nombre en singular —«Task», no «Tasks»— porque es el que emiten los comandos al
    /// pedir autorización (<c>IAuthorizeEntity.EntityType</c>). Es el vocabulario único de la
    /// tabla; ver <see cref="Identity.Domain.Permisos.TiposDePermiso"/>.
    /// </summary>
    public static string ComoLoLlamaElPermiso(string tipoDeEntidad) => tipoDeEntidad switch
    {
        TiposDeEntidad.Tarea => Identity.Domain.Permisos.TiposDePermiso.Tarea,
        TiposDeEntidad.Proyecto => Identity.Domain.Permisos.TiposDePermiso.Proyecto,
        TiposDeEntidad.Ticket => Identity.Domain.Permisos.TiposDePermiso.Ticket,
        TiposDeEntidad.Documento => Identity.Domain.Permisos.TiposDePermiso.Documento,
        _ => tipoDeEntidad
    };
}

/// <summary>Compartir con alguien, o cambiarle el nivel si ya lo tenía.</summary>
public sealed record CompartirCommand(
    Guid TenantId,
    string Tipo,
    Guid EntityId,
    Guid ConUsuarioId,
    string Nivel) : ICommand<bool>;

/// <summary>Dejar de compartir con alguien.</summary>
public sealed record DejarDeCompartirCommand(
    Guid TenantId,
    string Tipo,
    Guid EntityId,
    Guid ConUsuarioId) : ICommand<bool>;

/// <summary>Con quién está compartido, para pintarlo.</summary>
public sealed record GetCompartidoConQuery(
    Guid TenantId,
    string Tipo,
    Guid EntityId) : IQuery<IReadOnlyList<Guid>>;

public sealed class CompartirHandler(
    IRepositorioDeComparticion repositorio,
    IIdentityUnitOfWork unitOfWork) : ICommandHandler<CompartirCommand, bool>
{
    /// <summary>Los niveles que la tabla de permisos entiende; cualquier otro se rechaza.</summary>
    private static readonly string[] NivelesValidos = ["View", "Edit", "Full"];

    public async Task<Result<bool>> Handle(CompartirCommand request, CancellationToken ct)
    {
        if (!TiposDeEntidad.Existe(request.Tipo))
            return Result<bool>.Failure("Ese tipo de elemento no se puede compartir");

        if (!NivelesValidos.Contains(request.Nivel))
            return Result<bool>.Failure($"El nivel debe ser uno de: {string.Join(", ", NivelesValidos)}");

        if (request.EntityId == Guid.Empty)
            return Result<bool>.Failure("Falta el elemento que se quiere compartir");

        var tipoEnPermisos = VocabularioDePermisos.ComoLoLlamaElPermiso(request.Tipo);

        var existente = await repositorio.BuscarAsync(
            request.TenantId, request.ConUsuarioId, tipoEnPermisos, request.EntityId, ct);

        if (existente is not null)
        {
            // Compartir dos veces con la misma persona cambia el nivel, no crea una fila
            // paralela. Con dos filas, cuál gana dependería del orden de lectura.
            existente.UpdatePermissionLevel(request.Nivel);
        }
        else
        {
            await repositorio.AnadirAsync(
                EntityPermission.CreateForUser(
                    request.TenantId, request.ConUsuarioId, tipoEnPermisos, request.EntityId, request.Nivel),
                ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public sealed class DejarDeCompartirHandler(
    IRepositorioDeComparticion repositorio,
    IIdentityUnitOfWork unitOfWork) : ICommandHandler<DejarDeCompartirCommand, bool>
{
    public async Task<Result<bool>> Handle(DejarDeCompartirCommand request, CancellationToken ct)
    {
        var existente = await repositorio.BuscarAsync(
            request.TenantId, request.ConUsuarioId,
            VocabularioDePermisos.ComoLoLlamaElPermiso(request.Tipo), request.EntityId, ct);

        // Dejar de compartir algo que ya no está compartido no es un error: es el estado que se
        // pedía. Devolver un fallo obligaría a la pantalla a distinguir dos casos idénticos.
        if (existente is null)
            return Result<bool>.Success(true);

        repositorio.Quitar(existente);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public sealed class GetCompartidoConHandler(IRepositorioDeComparticion repositorio)
    : IQueryHandler<GetCompartidoConQuery, IReadOnlyList<Guid>>
{
    public async Task<Result<IReadOnlyList<Guid>>> Handle(GetCompartidoConQuery request, CancellationToken ct)
        => Result<IReadOnlyList<Guid>>.Success(
            await repositorio.ConQuienAsync(
                request.TenantId,
                VocabularioDePermisos.ComoLoLlamaElPermiso(request.Tipo),
                request.EntityId, ct));
}

/// <summary>
/// Acceso a las filas de permiso que representan una compartición nominal.
///
/// Sólo mira filas con <c>EntityId</c> real y <c>UserId</c> real: los permisos por rol y los de
/// módulo entero viven en la misma tabla y no son comparticiones.
/// </summary>
public interface IRepositorioDeComparticion
{
    Task<EntityPermission?> BuscarAsync(Guid tenantId, Guid userId, string tipoEnPermisos, Guid entityId, CancellationToken ct);

    Task<IReadOnlyList<Guid>> ConQuienAsync(Guid tenantId, string tipoEnPermisos, Guid entityId, CancellationToken ct);

    Task<IReadOnlyList<Guid>> CompartidosConAsync(Guid tenantId, Guid userId, string tipoEnPermisos, CancellationToken ct);

    Task<IReadOnlyList<Guid>> CompartidosConAlguienAsync(Guid tenantId, string tipoEnPermisos, CancellationToken ct);

    Task AnadirAsync(EntityPermission permiso, CancellationToken ct);

    void Quitar(EntityPermission permiso);
}
