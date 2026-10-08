using BuildingBlocks.Domain;

namespace Projects.Application.Abstractions;

/// <summary>
/// UnitOfWork del módulo Projects.
///
/// Existe para que los handlers no dependan del <c>IUnitOfWork</c> genérico. Nueve módulos
/// lo registraban en el mismo contenedor, así que ganaba el último y todos los handlers
/// acababan guardando en el <c>DbContext</c> de otro módulo: la petición respondía bien y no
/// escribía nada. Con una interfaz por módulo, equivocarse deja de compilar.
/// </summary>
public interface IProjectsUnitOfWork : IUnitOfWork
{
    /// <summary>
    /// Guarda, deja los eventos en el outbox y además los reparte en proceso por MediatR. Ver
    /// <c>IWorkItemsUnitOfWork.SaveChangesAndDispatchAsync</c>: sin esto, la etiqueta automática
    /// de cada proyecto nuevo estaba escrita y no se creaba nunca.
    /// </summary>
    Task<int> SaveChangesAndDispatchAsync(CancellationToken ct = default);
}
