using Automations.Application.Abstractions;
using Automations.Domain.ValueObjects;
using BuildingBlocks.Application.Events;
using MediatR;
using WorkItems.Application.Commands;
using WorkItems.Domain.Events;

namespace ApiHost.Services;

/// <summary>
/// Traduce lo que pasa en las tareas a disparos de automatización, y las acciones de vuelta a
/// comandos de tareas.
///
/// **Vive en el host a propósito.** Ningún módulo de este producto referencia a otro, y el de
/// automatizaciones no conoce a WorkItems ni al revés: uno define qué acciones existen y el otro
/// sabe cambiar un estado. El único sitio que ya los conoce a los dos es el host, que es
/// exactamente lo que compone una aplicación modular. Meter la referencia dentro del módulo
/// habría sido el primer paso para que dejaran de ser módulos.
/// </summary>
public sealed class TaskActionExecutor(IMediator mediator, AutomationNotifier notifier) : IActionExecutor
{
  public async Task RunAsync(
      Guid tenantId, Guid entityId, string actionType, string value, CancellationToken ct = default)
  {
    // Avisar no es un cambio en la tarea, así que no pasa por el comando de parcheo: sale por
    // su propio camino, que además consulta las preferencias de quien va a recibirlo.
    if (actionType == ActionTypes.Notify)
    {
      await notifier.NotifyAsync(tenantId, entityId, value, ct);
      return;
    }

    // El actor es el sistema: la acción no la hace una persona, la hace una regla que alguien
    // configuró antes. Poner aquí al usuario que movió la tarea le atribuiría cambios que no hizo.
    var command = new PatchTaskCommand(
        TenantId: tenantId,
        Id: entityId,
        ActorId: Guid.Empty,
        ActorRole: "Automation",
        Title: null,
        Description: null,
        Status: actionType == ActionTypes.ChangeStatus ? value : null,
        Priority: actionType == ActionTypes.ChangePriority ? value : null,
        AssigneeId: actionType == ActionTypes.AssignTo && Guid.TryParse(value, out var assignee)
            ? assignee
            : null,
        DueDate: null,
        EstimatedHours: null);

    var result = await mediator.Send(command, ct);

    // Un rechazo del dominio —un estado que ya no existe— tiene que llegar al motor para que lo
    // registre. Tragárselo aquí dejaría la automatización contada como ejecutada sin haber hecho
    // nada, que es la clase de mentira que este proyecto persigue.
    if (!result.IsSuccess)
      throw new InvalidOperationException(result.Error);
  }
}

/// <summary>
/// Escucha los eventos de tareas y llama al motor.
///
/// Los tres disparadores que hoy existen se corresponden con tres eventos que WorkItems ya
/// emitía desde la 4A. No se ha añadido ninguno: un disparador que no esté conectado a un evento
/// real dejaría configurar automatizaciones que no se ejecutan nunca.
/// </summary>
public sealed class AutomationsBridge(IAutomationEngine motor) :
    INotificationHandler<DomainEventNotification<TaskCreatedEvent>>,
    INotificationHandler<DomainEventNotification<TaskStatusChangedEvent>>,
    INotificationHandler<DomainEventNotification<TaskPriorityChangedEvent>>
{
  public Task Handle(DomainEventNotification<TaskCreatedEvent> notification, CancellationToken ct)
  {
    var e = notification.DomainEvent;

    return motor.RunAsync(new AutomationTriggerEvent(
        e.TenantId, TriggerTypes.TaskCreated, e.TaskId,
        new Dictionary<string, string?>
        {
          [EventFields.ProjectId] = e.ProjectId.ToString(),
          [EventFields.AssigneeId] = e.AssigneeId == Guid.Empty ? null : e.AssigneeId.ToString(),
        }), ct);
  }

  public Task Handle(DomainEventNotification<TaskStatusChangedEvent> notification, CancellationToken ct)
  {
    var e = notification.DomainEvent;

    return motor.RunAsync(new AutomationTriggerEvent(
        e.TenantId, TriggerTypes.TaskStatusChanged, e.TaskId,
        new Dictionary<string, string?>
        {
          [EventFields.Status] = e.NewStatus,
          [EventFields.PreviousStatus] = e.OldStatus,
          [EventFields.ProjectId] = e.ProjectId.ToString(),
        }), ct);
  }

  public Task Handle(DomainEventNotification<TaskPriorityChangedEvent> notification, CancellationToken ct)
  {
    var e = notification.DomainEvent;

    return motor.RunAsync(new AutomationTriggerEvent(
        e.TenantId, TriggerTypes.TaskPriorityChanged, e.TaskId,
        new Dictionary<string, string?>
        {
          [EventFields.Priority] = e.NewPriority,
          [EventFields.PreviousPriority] = e.OldPriority,
          [EventFields.ProjectId] = e.ProjectId.ToString(),
        }), ct);
  }
}
