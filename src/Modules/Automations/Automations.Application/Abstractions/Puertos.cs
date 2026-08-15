using Automations.Domain.Entities;

namespace Automations.Application.Abstractions;

public interface IAutomationRuleRepository
{
    Task<AutomationRule?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<AutomationRule>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Las reglas activas de un disparador, que es lo que el motor pide en cada evento.</summary>
    Task<IReadOnlyList<AutomationRule>> GetActivasPorDisparadorAsync(
        Guid tenantId, string disparador, CancellationToken ct = default);

    Task<bool> ExisteConNombreAsync(Guid tenantId, string nombre, Guid? excepto, CancellationToken ct = default);

    Task AddAsync(AutomationRule regla, CancellationToken ct = default);
    Task UpdateAsync(AutomationRule regla, CancellationToken ct = default);
    Task RemoveAsync(AutomationRule regla, CancellationToken ct = default);
}

public interface IAutomationsUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>
/// Lo que ha pasado, contado en términos que este módulo entiende.
///
/// Es deliberadamente neutro —un nombre de disparador, un identificador y un diccionario— para
/// que el módulo de automatizaciones **no tenga que conocer a WorkItems**. Ningún módulo de este
/// producto referencia a otro; quien traduce el evento de tareas a este disparo es el host, que
/// es el único sitio que ya los conoce a todos.
/// </summary>
public sealed record DisparoDeAutomatizacion(
    Guid TenantId,
    string Disparador,
    Guid EntityId,
    IReadOnlyDictionary<string, string?> Datos);

public interface IMotorDeAutomatizaciones
{
    /// <summary>Ejecuta las reglas que apliquen y devuelve cuántas se ejecutaron.</summary>
    Task<int> EjecutarAsync(DisparoDeAutomatizacion disparo, CancellationToken ct = default);
}

/// <summary>
/// Quien sabe llevar a cabo una acción sobre la entidad que disparó la regla.
///
/// El módulo define qué acciones existen; **cómo se aplican vive fuera**, por la misma razón: la
/// acción «cambiar el estado» es un comando de WorkItems, y este módulo no lo conoce.
/// </summary>
public interface IEjecutorDeAcciones
{
    Task EjecutarAsync(
        Guid tenantId, Guid entityId, string tipoDeAccion, string valor, CancellationToken ct = default);
}
