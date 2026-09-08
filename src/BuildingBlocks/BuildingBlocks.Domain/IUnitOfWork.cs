namespace BuildingBlocks.Domain;

public interface IUnitOfWork
{
    /// <summary>
    /// Guarda y deja los eventos de dominio en el outbox, para que salgan por el bus.
    ///
    /// **No los reparte en proceso.** Quien necesite que otro handler reaccione dentro de la
    /// misma petición —una automatización que cambia la tarea, un aviso por SignalR que tiene que
    /// llegar antes de que la pantalla se refresque— necesita un UnitOfWork que además despache;
    /// ver <c>IWorkItemsUnitOfWork</c>.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
