using BuildingBlocks.Domain;

namespace Ticketing.Application.Abstractions;

/// <summary>
/// UnitOfWork del módulo Ticketing.
///
/// Existe para que los handlers no dependan del <c>IUnitOfWork</c> genérico. Nueve módulos
/// lo registraban en el mismo contenedor, así que ganaba el último y todos los handlers
/// acababan guardando en el <c>DbContext</c> de otro módulo: la petición respondía bien y no
/// escribía nada. Con una interfaz por módulo, equivocarse deja de compilar.
/// </summary>
public interface ITicketingUnitOfWork : IUnitOfWork
{
    /// <summary>
    /// Guarda, deja los eventos en el outbox y además los reparte en proceso por MediatR.
    /// Ver <c>IWorkItemsUnitOfWork.SaveChangesAndDispatchAsync</c>: aquí faltaba lo mismo, y el
    /// aviso por SignalR de los tickets que cambian de estado estaba escrito sin ejecutarse nunca.
    /// </summary>
    Task<int> SaveChangesAndDispatchAsync(CancellationToken ct = default);
}
