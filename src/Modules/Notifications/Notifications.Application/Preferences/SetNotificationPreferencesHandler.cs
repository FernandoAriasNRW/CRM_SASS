using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Notifications.Domain.Entities;

namespace Notifications.Application.Preferences;

public sealed class SetNotificationPreferencesHandler(INotificationPreferencesRepository repository)
    : ICommandHandler<SetNotificationPreferencesCommand, NotificationPreferencesDto>
{
    public async Task<Result<NotificationPreferencesDto>> Handle(SetNotificationPreferencesCommand request, CancellationToken ct)
    {
        // Las horas llegan como texto desde un <input type="time">. Si no se pueden leer se
        // rechaza la petición entera en vez de sustituirlas por un valor razonable: guardar en
        // silencio unas horas de silencio distintas de las que la persona escribió es la clase
        // de fallo que se descubre semanas después, al no recibir un aviso.
        if (!TimeOnly.TryParse(request.QuietHoursStart, out var start))
            return Result<NotificationPreferencesDto>.Failure($"La hora de inicio del silencio no es válida: '{request.QuietHoursStart}'");

        if (!TimeOnly.TryParse(request.QuietHoursEnd, out var end))
            return Result<NotificationPreferencesDto>.Failure($"La hora de fin del silencio no es válida: '{request.QuietHoursEnd}'");

        // Un tramo de silencio de longitud cero no silencia nada, así que activarlo con las dos
        // horas iguales sólo puede ser un error de quien lo rellenó.
        if (request.QuietHoursEnabled && start == end)
            return Result<NotificationPreferencesDto>.Failure("Las horas de silencio no pueden empezar y terminar a la misma hora");

        var preferences = await repository.GetForUserAsync(request.TenantId, request.UserId, ct);

        if (preferences is null)
        {
            preferences = NotificationPreferences.CreateDefault(request.TenantId, request.UserId);
            await repository.AddAsync(preferences, ct);
        }

        preferences.Update(
            request.EmailEnabled, request.PushEnabled,
            request.TaskAssigned, request.TaskDueSoon, request.MentionEnabled, request.ExportReady,
            request.TaskCompleted, request.TicketCreated, request.TicketUpdated, request.ProjectUpdated,
            request.QuietHoursEnabled, start, end);

        await repository.SaveAsync(ct);

        // Se devuelve lo guardado, no lo recibido. El `PUT` anterior devolvía el cuerpo de la
        // petición tal cual, así que la pantalla confirmaba cambios que nunca llegaron a la
        // base: parecía funcionar hasta que alguien recargaba.
        return Result<NotificationPreferencesDto>.Success(NotificationPreferencesDto.From(preferences));
    }
}
