using BuildingBlocks.Domain.Primitives;

namespace Automations.Domain.Events;

public sealed record AutomationRuleDefinedEvent(
    Guid RuleId, Guid TenantId, string Name, string Trigger) : DomainEvent;

public sealed record AutomationRuleUpdatedEvent(
    Guid RuleId, Guid TenantId, string Name) : DomainEvent;

/// <summary>
/// Una regla se ejecutó sobre una entidad.
///
/// Se emite para dejar rastro —quién tocó esta tarea y por qué— pero **ninguna regla lo escucha**:
/// las acciones de una automatización no disparan otras automatizaciones. Ver
/// <see cref="Entities.AutomationRule"/>.
/// </summary>
public sealed record AutomationRuleExecutedEvent(
    Guid RuleId, Guid TenantId, Guid EntityId, int Actions) : DomainEvent;
