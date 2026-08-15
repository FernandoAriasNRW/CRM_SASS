using BuildingBlocks.Domain;

namespace WorkItems.Application.Abstractions;

/// <summary>
/// UnitOfWork del módulo WorkItems.
///
/// Existe para que los handlers no dependan del <c>IUnitOfWork</c> genérico. Nueve módulos
/// lo registraban en el mismo contenedor, así que ganaba el último y todos los handlers
/// acababan guardando en el <c>DbContext</c> de otro módulo: la petición respondía bien y no
/// escribía nada. Con una interfaz por módulo, equivocarse deja de compilar.
/// </summary>
public interface IWorkItemsUnitOfWork : IUnitOfWork
{
    /// <summary>
    /// Guarda, deja los eventos en el outbox **y además los reparte en proceso** por MediatR.
    ///
    /// La implementación lo hacía desde el principio, pero ningún contrato lo ofrecía, así que
    /// ningún handler podía pedirlo: los `INotificationHandler` escritos para los eventos de
    /// tarea —el aviso al tablero por SignalR entre ellos— **no se ejecutaban nunca**. Se
    /// descubrió al conectar el motor de automatizaciones, que depende justo de eso.
    ///
    /// Se declara aquí y no en el `IUnitOfWork` de todos los módulos porque tres módulos tienen
    /// un UnitOfWork propio que ni siquiera escribe en el outbox: obligarlos a ofrecer un método
    /// que no saben cumplir habría dado una implementación de mentira, que es peor que no
    /// tenerlo.
    /// </summary>
    Task<int> SaveChangesAndDispatchAsync(CancellationToken ct = default);
}
