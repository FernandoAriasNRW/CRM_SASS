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
public sealed class EjecutorDeAccionesDeTareas(IMediator mediator) : IEjecutorDeAcciones
{
  public async Task EjecutarAsync(
      Guid tenantId, Guid entityId, string tipoDeAccion, string valor, CancellationToken ct = default)
  {
    // El actor es el sistema: la acción no la hace una persona, la hace una regla que alguien
    // configuró antes. Poner aquí al usuario que movió la tarea le atribuiría cambios que no hizo.
    var comando = new PatchTaskCommand(
        TenantId: tenantId,
        Id: entityId,
        ActorId: Guid.Empty,
        ActorRole: "Automation",
        Title: null,
        Description: null,
        Status: tipoDeAccion == TipoDeAccion.CambiarEstado ? valor : null,
        Priority: tipoDeAccion == TipoDeAccion.CambiarPrioridad ? valor : null,
        AssigneeId: tipoDeAccion == TipoDeAccion.AsignarA && Guid.TryParse(valor, out var asignado)
            ? asignado
            : null,
        DueDate: null,
        EstimatedHours: null);

    var resultado = await mediator.Send(comando, ct);

    // Un rechazo del dominio —un estado que ya no existe— tiene que llegar al motor para que lo
    // registre. Tragárselo aquí dejaría la automatización contada como ejecutada sin haber hecho
    // nada, que es la clase de mentira que este proyecto persigue.
    if (!resultado.IsSuccess)
      throw new InvalidOperationException(resultado.Error);
  }
}

/// <summary>
/// Escucha los eventos de tareas y llama al motor.
///
/// Los tres disparadores que hoy existen se corresponden con tres eventos que WorkItems ya
/// emitía desde la 4A. No se ha añadido ninguno: un disparador que no esté conectado a un evento
/// real dejaría configurar automatizaciones que no se ejecutan nunca.
/// </summary>
public sealed class PuenteDeAutomatizaciones(IMotorDeAutomatizaciones motor) :
    INotificationHandler<DomainEventNotification<TaskCreatedEvent>>,
    INotificationHandler<DomainEventNotification<TaskStatusChangedEvent>>,
    INotificationHandler<DomainEventNotification<TaskPriorityChangedEvent>>
{
  public Task Handle(DomainEventNotification<TaskCreatedEvent> notificacion, CancellationToken ct)
  {
    var e = notificacion.DomainEvent;

    return motor.EjecutarAsync(new DisparoDeAutomatizacion(
        e.TenantId, TipoDeDisparador.TareaCreada, e.TaskId,
        new Dictionary<string, string?>
        {
          [CampoDelEvento.ProyectoId] = e.ProjectId.ToString(),
          [CampoDelEvento.ResponsableId] = e.AssigneeId == Guid.Empty ? null : e.AssigneeId.ToString(),
        }), ct);
  }

  public Task Handle(DomainEventNotification<TaskStatusChangedEvent> notificacion, CancellationToken ct)
  {
    var e = notificacion.DomainEvent;

    return motor.EjecutarAsync(new DisparoDeAutomatizacion(
        e.TenantId, TipoDeDisparador.TareaCambiaDeEstado, e.TaskId,
        new Dictionary<string, string?>
        {
          [CampoDelEvento.Estado] = e.NewStatus,
          [CampoDelEvento.EstadoAnterior] = e.OldStatus,
          [CampoDelEvento.ProyectoId] = e.ProjectId.ToString(),
        }), ct);
  }

  public Task Handle(DomainEventNotification<TaskPriorityChangedEvent> notificacion, CancellationToken ct)
  {
    var e = notificacion.DomainEvent;

    return motor.EjecutarAsync(new DisparoDeAutomatizacion(
        e.TenantId, TipoDeDisparador.TareaCambiaDePrioridad, e.TaskId,
        new Dictionary<string, string?>
        {
          [CampoDelEvento.Prioridad] = e.NewPriority,
          [CampoDelEvento.PrioridadAnterior] = e.OldPriority,
          [CampoDelEvento.ProyectoId] = e.ProjectId.ToString(),
        }), ct);
  }
}
