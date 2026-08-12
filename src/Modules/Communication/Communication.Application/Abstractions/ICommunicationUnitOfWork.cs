using BuildingBlocks.Domain;

namespace Communication.Application.Abstractions;

/// <summary>
/// UnitOfWork del módulo Communication.
///
/// Existe para que los handlers no dependan del <c>IUnitOfWork</c> genérico. Nueve módulos
/// lo registraban en el mismo contenedor, así que ganaba el último y todos los handlers
/// acababan guardando en el <c>DbContext</c> de otro módulo: la petición respondía bien y no
/// escribía nada. Con una interfaz por módulo, equivocarse deja de compilar.
/// </summary>
public interface ICommunicationUnitOfWork : IUnitOfWork
{
}
