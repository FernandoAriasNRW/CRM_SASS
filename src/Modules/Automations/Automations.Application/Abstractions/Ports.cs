using Automations.Domain.Entities;

namespace Automations.Application.Abstractions;

public interface IAutomationRuleRepository
{
    Task<AutomationRule?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<AutomationRule>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Las reglas activas de un disparador, que es lo que el motor pide en cada evento.</summary>
    Task<IReadOnlyList<AutomationRule>> GetActiveByTriggerAsync(
        Guid tenantId, string trigger, CancellationToken ct = default);

    Task<bool> ExistsWithNameAsync(Guid tenantId, string name, Guid? exceptId, CancellationToken ct = default);

    Task AddAsync(AutomationRule rule, CancellationToken ct = default);
    Task UpdateAsync(AutomationRule rule, CancellationToken ct = default);
    Task RemoveAsync(AutomationRule rule, CancellationToken ct = default);
}

/// <summary>
/// El registro de ejecuciones. Ver <see cref="AutomationExecution"/> para qué resuelve.
/// </summary>
public interface IExecutionRepository
{
    Task RecordAsync(AutomationExecution execution, CancellationToken ct = default);

    /// <summary>
    /// Si esa regla ya se ejecutó hoy sobre esa entidad.
    ///
    /// Es lo que impide que el disparador por vencimiento avise todos los días sobre la misma
    /// tarea. Se pregunta por día y no por un rango de horas para que la comparación sea una
    /// igualdad sobre una columna de fecha, indexable y sin líos de zona horaria.
    /// </summary>
    Task<bool> AlreadyRanTodayAsync(
        Guid tenantId, Guid ruleId, Guid entityId, DateOnly day, CancellationToken ct = default);

    /// <summary>Las últimas ejecuciones de una regla, de la más reciente a la más antigua.</summary>
    Task<IReadOnlyList<AutomationExecution>> LatestForRuleAsync(
        Guid tenantId, Guid ruleId, int count, CancellationToken ct = default);
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
public sealed record AutomationTriggerEvent(
    Guid TenantId,
    string Trigger,
    Guid EntityId,
    IReadOnlyDictionary<string, string?> Data);

public interface IAutomationEngine
{
    /// <summary>Ejecuta las reglas que apliquen y devuelve cuántas se ejecutaron.</summary>
    Task<int> RunAsync(AutomationTriggerEvent triggerEvent, CancellationToken ct = default);
}

/// <summary>
/// Quien sabe llevar a cabo una acción sobre la entidad que disparó la regla.
///
/// El módulo define qué acciones existen; **cómo se aplican vive fuera**, por la misma razón: la
/// acción «cambiar el estado» es un comando de WorkItems, y este módulo no lo conoce.
/// </summary>
public interface IActionExecutor
{
    Task RunAsync(
        Guid tenantId, Guid entityId, string actionType, string value, CancellationToken ct = default);
}
