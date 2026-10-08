using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Notifications.Domain.Entities;

namespace Notifications.Application.Preferences;

/// <summary>
/// Devuelve las preferencias de quien pregunta, creándolas por defecto si nunca las tocó.
///
/// **No se guardan al leerlas.** Consultar no es modificar, y escribir en una petición de
/// lectura convierte cada apertura de la pantalla en una escritura, con el añadido de que dos
/// pestañas abiertas a la vez pueden crear dos filas. Se materializan al guardar por primera
/// vez; hasta entonces, quien pregunta recibe las de por defecto y nadie nota la diferencia.
/// </summary>
public sealed class GetNotificationPreferencesHandler(INotificationPreferencesRepository repository)
    : IQueryHandler<GetNotificationPreferencesQuery, NotificationPreferencesDto>
{
    public async Task<Result<NotificationPreferencesDto>> Handle(GetNotificationPreferencesQuery request, CancellationToken ct)
    {
        var saved = await repository.GetForUserAsync(request.TenantId, request.UserId, ct);

        return Result<NotificationPreferencesDto>.Success(NotificationPreferencesDto.From(
            saved ?? NotificationPreferences.CreateDefault(request.TenantId, request.UserId), request.IsAdmin));
    }
}
