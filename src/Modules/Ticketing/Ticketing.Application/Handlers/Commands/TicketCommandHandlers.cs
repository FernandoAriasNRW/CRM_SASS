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