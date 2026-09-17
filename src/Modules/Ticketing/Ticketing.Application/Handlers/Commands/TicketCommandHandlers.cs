using BuildingBlocks.Application.Abstractions;
using Ticketing.Application.Abstractions;
using BuildingBlocks.Domain;
using Ticketing.Application.Abstractions.Repositories;
using Ticketing.Application.Commands;
using Ticketing.Domain.Entities;
using Ticketing.Domain.ValueObjects;

namespace Ticketing.Application.Handlers.Commands;

public sealed class CreateTicketHandler(
    ITicketRepository repository,
    ITicketingUnitOfWork unitOfWork) : ICommandHandler<CreateTicketCommand, Ticket>
{
  public async Task<Result<Ticket>> Handle(CreateTicketCommand request, CancellationToken cancellationToken)
  {
    var priority = TicketPriority.FromName<TicketPriority>(request.Priority);
    if (priority is null)
      return Result<Ticket>.Failure("Invalid priority");

    var ticketResult = Ticket.Create(request.TenantId, request.CustomerId,
        request.Title, request.Description, priority);

    if (ticketResult.IsFailure)
      return Result<Ticket>.Failure(ticketResult.Error!);

    var ticket = ticketResult.Value!;
    await repository.AddAsync(ticket, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return Result<Ticket>.Success(ticket);
  }
}

/// <summary>
/// La edición de un ticket: título, descripción, prioridad, estado y responsable.
///
/// <b>No existía.</b> El endpoint <c>PATCH /tickets/{id}</c> estaba publicado y la pantalla lo
/// usaba —al guardar la ficha y al arrastrar una tarjeta entre columnas— pero MediatR no tenía a
/// quién entregarle el comando, así que cada intento acababa en un 500 con «No service for type
/// IRequestHandler». El tablero devolvía la tarjeta a su sitio y decía «no se pudo mover», sin
/// más pista.
///
/// Acepta campos sueltos a propósito. Arrastrar manda sólo el estado, y la ficha manda los
/// cuatro; un comando por campo multiplicaría los viajes sin ganar nada.
/// </summary>
public sealed class UpdateTicketHandler(
    ITicketRepository repository,
    ITicketingUnitOfWork unitOfWork) : ICommandHandler<UpdateTicketCommand, bool>
{
  public async Task<Result<bool>> Handle(UpdateTicketCommand request, CancellationToken cancellationToken)
  {
    var ticket = await repository.GetByIdAsync(request.TenantId, request.TicketId, cancellationToken);
    if (ticket is null)
      return Result<bool>.Failure("Ticket not found");

    TicketPriority? priority = null;
    if (!string.IsNullOrWhiteSpace(request.Priority))
    {
      priority = TicketPriority.FromName<TicketPriority>(request.Priority);
      if (priority is null)
        return Result<bool>.Failure($"Invalid priority: {request.Priority}");
    }

    // Se valida el estado **antes** de tocar nada. Si se aplicaran los campos uno a uno y el
    // estado resultara inválido, el ticket se quedaría con el título nuevo y el estado viejo:
    // medio guardado, y la pantalla enseñando un error como si no se hubiera guardado nada.
    TicketStatus? status = null;
    if (!string.IsNullOrWhiteSpace(request.Status))
    {
      status = TicketStatus.FromName<TicketStatus>(request.Status);
      if (status is null)
        return Result<bool>.Failure($"Invalid status: {request.Status}");
    }

    var updated = ticket.Update(request.Title, request.Description, priority);
    if (updated.IsFailure)
      return Result<bool>.Failure(updated.Error!);

    if (status is not null && status.Value != ticket.Status.Value)
    {
      // Por el método del dominio, no asignando el valor: es el que levanta el evento que
      // disparan las automatizaciones y las notificaciones de «tu ticket cambió de estado».
      if (!ticket.ChangeStatus(status))
        return Result<bool>.Failure($"Cannot transition from {ticket.Status.Name} to {request.Status}");
    }

    if (request.Classification is not null)
    {
      if (request.Classification.Trim().Length > 100)
        return Result<bool>.Failure("La clasificación admite hasta 100 caracteres");
      ticket.Classify(request.Classification);
    }

    if (request.TeamId is not null)
      ticket.AssignTeam(request.TeamId);

    if (request.Tags is not null)
      ticket.ChangeTags(request.Tags);

    if (request.AssignedAgentId is not null)
    {
      if (request.AssignedAgentId == Guid.Empty)
        ticket.Unassign();
      else if (request.AssignedAgentId != ticket.AssignedAgentId)
        ticket.AssignTo(request.AssignedAgentId.Value);
    }

    await repository.UpdateAsync(ticket, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    return Result<bool>.Success(true);
  }
}

public sealed class ChangeTicketStatusHandler(
    ITicketRepository repository,
    ITicketingUnitOfWork unitOfWork) : ICommandHandler<ChangeTicketStatusCommand, bool>
{
  public async Task<Result<bool>> Handle(ChangeTicketStatusCommand request, CancellationToken cancellationToken)
  {
    var ticket = await repository.GetByIdAsync(request.TenantId, request.TicketId, cancellationToken);
    if (ticket is null)
      return Result<bool>.Failure("Ticket not found");

    var newStatus = TicketStatus.FromName<TicketStatus>(request.NewStatus);
    if (newStatus is null)
      return Result<bool>.Failure("Invalid status");

    // Domain method validates transition rules and raises TicketStatusChangedEvent
    if (!ticket.ChangeStatus(newStatus))
      return Result<bool>.Failure($"Cannot transition from {ticket.Status.Name} to {request.NewStatus}");

    await repository.UpdateAsync(ticket, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    return Result<bool>.Success(true);
  }
}

public sealed class AssignTicketHandler(
    ITicketRepository repository,
    ITicketingUnitOfWork unitOfWork) : ICommandHandler<AssignTicketCommand, bool>
{
  public async Task<Result<bool>> Handle(AssignTicketCommand request, CancellationToken cancellationToken)
  {
    var ticket = await repository.GetByIdAsync(request.TenantId, request.TicketId, cancellationToken);
    if (ticket is null)
      return Result<bool>.Failure("Ticket not found");

    // Domain method raises TicketAssignedEvent
    ticket.AssignTo(request.AgentId);

    await repository.UpdateAsync(ticket, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    return Result<bool>.Success(true);
  }
}

public sealed class CloseTicketHandler(
    ITicketRepository repository,
    ITicketingUnitOfWork unitOfWork) : ICommandHandler<CloseTicketCommand, bool>
{
  public async Task<Result<bool>> Handle(CloseTicketCommand request, CancellationToken cancellationToken)
  {
    var ticket = await repository.GetByIdAsync(request.TenantId, request.TicketId, cancellationToken);
    if (ticket is null)
      return Result<bool>.Failure("Ticket not found");

    if (!ticket.ChangeStatus(TicketStatus.Closed))
      return Result<bool>.Failure("Cannot close ticket in current status");

    await repository.UpdateAsync(ticket, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    return Result<bool>.Success(true);
  }
}