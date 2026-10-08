using ApiHost.Startup;
using Microsoft.AspNetCore.SignalR;
using Notifications.Application.DTOs;
using Notifications.Application.Sending;

namespace ApiHost.Notifications;

/// <summary>
/// Empuja un aviso a la pantalla de quien lo recibe, por el hub de notificaciones.
///
/// Va a la persona, no a un grupo: SignalR identifica cada conexión por el usuario del token
/// (<c>NameIdentifier</c>), y los identificadores de persona son únicos entre organizaciones, así
/// que un aviso no puede caer en la pantalla de otra.
///
/// La pantalla escuchaba <c>notification_received</c> desde el principio y nadie lo mandaba: los
/// avisos sólo aparecían al recargar.
/// </summary>
public sealed class NotificationPush(IHubContext<NotificationsHub> hub) : INotificationPush
{
    public Task PushAsync(Guid userId, NotificationDto notification, CancellationToken ct = default)
        => hub.Clients.User(userId.ToString()).SendAsync("notification_received", notification, ct);
}
