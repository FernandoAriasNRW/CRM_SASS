using BuildingBlocks.Domain.Primitives;
using WorkItems.Domain.Events;

namespace WorkItems.Domain.Entities;

/// <summary>
/// Una tarea bloqueada por otra.
///
/// Se lee así: <see cref="TaskId"/> **está bloqueada por** <see cref="DependsOnTaskId"/>. La
/// dirección importa y es la única que se guarda; «bloquea a» es la misma arista mirada del
/// otro lado, y duplicarla en la base sería garantizar que algún día las dos discrepen.
///
/// Es un agregado propio y no una colección dentro de <see cref="WorkTask"/>: la arista
/// pertenece a dos tareas por igual, y colgarla de una obligaría a cargar la otra para nada.
/// </summary>
public sealed class TaskDependency : AggregateRoot, ITenantEntity
{
    public Guid TenantId { get; private set; }

    /// <summary>La tarea que no puede avanzar.</summary>
    public Guid TaskId { get; private set; }

    /// <summary>La que tiene que resolverse antes.</summary>
    public Guid DependsOnTaskId { get; private set; }

    private TaskDependency() { }

    public static TaskDependency Create(Guid tenantId, Guid taskId, Guid dependsOnTaskId)
    {
        if (taskId == Guid.Empty || dependsOnTaskId == Guid.Empty)
            throw new InvalidOperationException("Las dos tareas de una dependencia son obligatorias");

        if (taskId == dependsOnTaskId)
            throw new InvalidOperationException(Reglas.NoPuedeBloquearseASiMisma);

        var dependencia = new TaskDependency
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TaskId = taskId,
            DependsOnTaskId = dependsOnTaskId
        };

        dependencia.RaiseDomainEvent(new TaskDependencyAddedEvent(dependencia.Id, tenantId, taskId, dependsOnTaskId));

        return dependencia;
    }

    /// <summary>
    /// Deja constancia de que la dependencia se retira, antes de borrarla.
    ///
    /// El evento lo emite el agregado y no el handler: quien lo cuenta es quien lo sabe, y así
    /// las automatizaciones de la 4D reciben la misma forma que en el alta.
    /// </summary>
    public void MarcarComoRetirada()
        => RaiseDomainEvent(new TaskDependencyRemovedEvent(Id, TenantId, TaskId, DependsOnTaskId));

    /// <summary>
    /// Los motivos por los que se rechaza una dependencia, en un solo sitio para que el
    /// dominio y el handler que consulta las otras filas digan lo mismo.
    /// </summary>
    public static class Reglas
    {
        public const string NoPuedeBloquearseASiMisma = "Una tarea no puede bloquearse a sí misma";
        public const string CrearariaUnCiclo = "La dependencia crearía un ciclo: la otra tarea ya depende de ésta, directa o indirectamente";
        public const string TareaNoExiste = "Alguna de las dos tareas no existe";
        public const string DeOtroProyecto = "Las dependencias sólo se pueden establecer entre tareas del mismo proyecto";
        public const string YaExiste = "Esa dependencia ya está registrada";
    }
}
