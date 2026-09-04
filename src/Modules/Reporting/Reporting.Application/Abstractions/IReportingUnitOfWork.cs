using BuildingBlocks.Domain;

namespace Reporting.Application.Abstractions;

/// <summary>
/// UnitOfWork del módulo Reporting.
///
/// Existe para que los handlers no dependan del <c>IUnitOfWork</c> genérico. Nueve módulos
/// lo registraban en el mismo contenedor, así que ganaba el último y todos los handlers
/// acababan guardando en el <c>DbContext</c> de otro módulo: la petición respondía bien y no
/// escribía nada. Con una interfaz por módulo, equivocarse deja de compilar.
/// </summary>
public interface IReportingUnitOfWork : IUnitOfWork
{
    /// <summary>
    /// Guarda, deja los eventos en el outbox <b>y además los reparte en proceso</b> por MediatR.
    ///
    /// Hace falta para que el aviso de «tu exportación está lista» llegue. El outbox no sirve
    /// para esto: publica en MassTransit, no en los <c>INotificationHandler</c> de MediatR, así
    /// que un evento que sólo pase por ahí no despierta a nadie dentro del proceso. Con
    /// <c>SaveChangesAsync</c> a secas, la exportación terminaba, el fichero quedaba guardado y
    /// <b>el aviso no salía nunca</b> — sin ningún error de por medio.
    ///
    /// Mismo razonamiento y misma forma que <c>IWorkItemsUnitOfWork</c>, donde este mismo hueco
    /// ya dejó sin ejecutar los avisos del tablero por SignalR.
    /// </summary>
    Task<int> SaveChangesAndDispatchAsync(CancellationToken ct = default);
}
