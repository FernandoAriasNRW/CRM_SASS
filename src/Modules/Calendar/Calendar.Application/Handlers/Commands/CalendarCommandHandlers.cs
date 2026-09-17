using BuildingBlocks.Application.Abstractions;
using Calendar.Application.Abstractions;
using BuildingBlocks.Domain;
using Calendar.Application.Abstractions.Repositories;
using Calendar.Application.Commands;
using Calendar.Application.DTOs;
using Calendar.Domain.Entities;
using Calendar.Domain.ValueObjects;

namespace Calendar.Application.Handlers.Commands;

/// <summary>
/// Handler para crear un nuevo evento de calendario.
/// </summary>
public sealed class CreateCalendarEventHandler(
    ICalendarEventRepository repository,
    ICalendarUnitOfWork unitOfWork) : ICommandHandler<CreateCalendarEventCommand, CalendarEventDto>
{
    private readonly ICalendarEventRepository _repository = repository;
    private readonly ICalendarUnitOfWork _unitOfWork = unitOfWork;

    public async Task<Result<CalendarEventDto>> Handle(CreateCalendarEventCommand request, CancellationToken ct)
    {
        // 1. Parsear y validar tipos
        var type = CalendarEventType.FromName(request.Type) ?? CalendarEventType.Meeting;
        var recurrence = RecurrencePattern.FromName(request.Recurrence) ?? RecurrencePattern.None;

        // 2. Delegar creación a la entidad (lógica de negocio en Domain)
        var createResult = CalendarEvent.Create(
            request.TenantId,
            request.OrganizerId,
            request.Title,
            request.StartTime,
            request.EndTime,
            type,
            request.ProjectId,
            request.TaskId,
            request.TicketId,
            request.Description,
            request.Location,
            request.IsAllDay,
            recurrence);

        if (createResult.IsFailure)
            return Result<CalendarEventDto>.Failure(createResult.Error!);

        var calendarEvent = createResult.Value;

        await _repository.AddAsync(calendarEvent!, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // 4. Retornar DTO
        return Result<CalendarEventDto>.Success(calendarEvent!.ToDto());
    }
}

/// <summary>
/// Handler para actualizar un evento de calendario.
/// </summary>
public sealed class UpdateCalendarEventHandler(
    ICalendarEventRepository repository,
    ICalendarUnitOfWork unitOfWork) : ICommandHandler<UpdateCalendarEventCommand, CalendarEventDto>
{
    private readonly ICalendarEventRepository _repository = repository;
    private readonly ICalendarUnitOfWork _unitOfWork = unitOfWork;

    public async Task<Result<CalendarEventDto>> Handle(UpdateCalendarEventCommand request, CancellationToken ct)
    {
        // 1. Obtener entidad (solo para comandos, no para queries)
        var calendarEvent = await _repository.GetByIdAsync(request.TenantId, request.EventId, ct);

        if (calendarEvent is null)
            return Result<CalendarEventDto>.Failure("Evento no encontrado");

        if (calendarEvent.IsDeleted)
            return Result<CalendarEventDto>.Failure("No se puede actualizar un evento eliminado");

        // 2. Delegar actualización a la entidad
        var updateResult = calendarEvent.Update(
            request.Title,
            request.StartTime,
            request.EndTime,
            request.Description,
            request.Location);

        if (updateResult.IsFailure)
            return Result<CalendarEventDto>.Failure(updateResult.Error!);

        // 3. Persistir
        await _repository.UpdateAsync(calendarEvent, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<CalendarEventDto>.Success(updateResult.Value!.ToDto());
    }
}

/// <summary>
/// Handler para reprogramar un evento de calendario.
/// </summary>
public sealed class RescheduleEventHandler(
    ICalendarEventRepository repository,
    ICalendarUnitOfWork unitOfWork) : ICommandHandler<RescheduleEventCommand, CalendarEventDto>
{
    private readonly ICalendarEventRepository _repository = repository;
    private readonly ICalendarUnitOfWork _unitOfWork = unitOfWork;

    public async Task<Result<CalendarEventDto>> Handle(RescheduleEventCommand request, CancellationToken ct)
    {
        var calendarEvent = await _repository.GetByIdAsync(request.TenantId, request.EventId, ct);

        if (calendarEvent is null)
            return Result<CalendarEventDto>.Failure("Evento no encontrado");

        var rescheduleResult = calendarEvent.Reschedule(request.NewStartTime, request.NewEndTime);

        if (rescheduleResult.IsFailure)
            return Result<CalendarEventDto>.Failure(rescheduleResult.Error!);

        await _repository.UpdateAsync(calendarEvent, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<CalendarEventDto>.Success(rescheduleResult.Value!.ToDto());
    }
}

/// <summary>
/// Handler para cancelar (soft delete) un evento de calendario.
/// </summary>
public sealed class CancelEventHandler(
    ICalendarEventRepository repository,
    ICalendarUnitOfWork unitOfWork) : ICommandHandler<CancelEventCommand, bool>
{
    private readonly ICalendarEventRepository _repository = repository;
    private readonly ICalendarUnitOfWork _unitOfWork = unitOfWork;

    public async Task<Result<bool>> Handle(CancelEventCommand request, CancellationToken ct)
    {
        var calendarEvent = await _repository.GetByIdAsync(request.TenantId, request.EventId, ct);

        if (calendarEvent is null)
            return Result<bool>.Failure("Evento no encontrado");

        if (calendarEvent.IsDeleted)
            return Result<bool>.Failure("El evento ya está en la papelera");

        // A la papelera. El comando se sigue llamando «Cancel» porque su nombre viaja en un
        // webhook publicado, pero lo que hace es esto: quitarlo de en medio, recuperable.
        calendarEvent.MoveToTrash(request.DeletedBy);

        await _repository.UpdateAsync(calendarEvent, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}

/// <summary>
/// Handler para restaurar un evento eliminado.
/// </summary>
public sealed class RestoreEventHandler(
    ICalendarEventRepository repository,
    ICalendarUnitOfWork unitOfWork) : ICommandHandler<RestoreEventCommand, CalendarEventDto>
{
    private readonly ICalendarEventRepository _repository = repository;
    private readonly ICalendarUnitOfWork _unitOfWork = unitOfWork;

    public async Task<Result<CalendarEventDto>> Handle(RestoreEventCommand request, CancellationToken ct)
    {
        var calendarEvent = await _repository.GetByIdAsync(
            request.TenantId,
            request.EventId,
            ct,
            includeDeleted: true);

        if (calendarEvent is null)
            return Result<CalendarEventDto>.Failure("Evento no encontrado");

        if (!calendarEvent.IsDeleted)
            return Result<CalendarEventDto>.Failure("El evento no está eliminado");

        calendarEvent.Restore();

        await _repository.UpdateAsync(calendarEvent, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<CalendarEventDto>.Success(calendarEvent.ToDto());
    }
}

/// <summary>
/// Handler para eliminar permanentemente un evento (hard delete).
/// Solo para administradores o limpieza de datos.
/// </summary>
public sealed class PermanentDeleteEventHandler(
    ICalendarEventRepository repository,
    ICalendarUnitOfWork unitOfWork) : ICommandHandler<PermanentDeleteEventCommand, bool>
{
    private readonly ICalendarEventRepository _repository = repository;
    private readonly ICalendarUnitOfWork _unitOfWork = unitOfWork;

    public async Task<Result<bool>> Handle(PermanentDeleteEventCommand request, CancellationToken ct)
    {
        var calendarEvent = await _repository.GetByIdAsync(
            request.TenantId,
            request.EventId,
            ct,
            includeDeleted: true);

        if (calendarEvent is null)
            return Result<bool>.Failure("Evento no encontrado");

        await _repository.DeleteAsync(request.TenantId, request.EventId, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}

/// <summary>
/// Anula un evento dejándolo a la vista, tachado. Ver <c>AnularEventoCommand</c> para por qué no
/// es lo mismo que mandarlo a la papelera.
/// </summary>
public sealed class AnularEventoHandler(
    ICalendarEventRepository repository,
    ICalendarUnitOfWork unitOfWork) : ICommandHandler<AnularEventoCommand, CalendarEventDto>
{
    public async Task<Result<CalendarEventDto>> Handle(AnularEventoCommand request, CancellationToken ct)
    {
        var evento = await repository.GetByIdAsync(request.TenantId, request.EventId, ct);

        if (evento is null)
            return Result<CalendarEventDto>.Failure("Evento no encontrado");

        var resultado = evento.Cancelar(request.PorQuien, request.Motivo);
        if (resultado.IsFailure)
            return Result<CalendarEventDto>.Failure(resultado.Error!);

        await repository.UpdateAsync(evento, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<CalendarEventDto>.Success(evento.ToDto());
    }
}

public sealed class ReactivarEventoHandler(
    ICalendarEventRepository repository,
    ICalendarUnitOfWork unitOfWork) : ICommandHandler<ReactivarEventoCommand, CalendarEventDto>
{
    public async Task<Result<CalendarEventDto>> Handle(ReactivarEventoCommand request, CancellationToken ct)
    {
        var evento = await repository.GetByIdAsync(request.TenantId, request.EventId, ct);

        if (evento is null)
            return Result<CalendarEventDto>.Failure("Evento no encontrado");

        var resultado = evento.Reactivar();
        if (resultado.IsFailure)
            return Result<CalendarEventDto>.Failure(resultado.Error!);

        await repository.UpdateAsync(evento, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<CalendarEventDto>.Success(evento.ToDto());
    }
}

/// <summary>
/// Enlaza el evento con un proyecto, una tarea o un ticket.
///
/// <b>No se comprueba que existan</b>, y es deliberado: cada uno vive en otro módulo y Calendar no
/// los referencia —esa es la regla de aislamiento de este proyecto—. Comprobarlo exigiría un
/// puerto por módulo; a cambio, un enlace a algo borrado se ve como un enlace que no lleva a
/// ninguna parte, que es un fallo visible y recuperable, no silencioso.
/// </summary>
public sealed class EnlazarEventoHandler(
    ICalendarEventRepository repository,
    ICalendarUnitOfWork unitOfWork) : ICommandHandler<EnlazarEventoCommand, CalendarEventDto>
{
    public async Task<Result<CalendarEventDto>> Handle(EnlazarEventoCommand request, CancellationToken ct)
    {
        var evento = await repository.GetByIdAsync(request.TenantId, request.EventId, ct);

        if (evento is null)
            return Result<CalendarEventDto>.Failure("Evento no encontrado");

        var resultado = evento.Enlazar(request.ProjectId, request.TaskId, request.TicketId);
        if (resultado.IsFailure)
            return Result<CalendarEventDto>.Failure(resultado.Error!);

        await repository.UpdateAsync(evento, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<CalendarEventDto>.Success(evento.ToDto());
    }
}
