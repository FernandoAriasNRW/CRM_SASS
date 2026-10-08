using BuildingBlocks.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Notifications.Application.Abstractions;
using Notifications.Application.Abstractions.Repositories;
using Notifications.Application.DTOs;
using Notifications.Application.Preferences;
using Notifications.Domain.Entities;

namespace Notifications.Application.Sending;

/// <summary>
/// Un aviso por mandar: de qué trata, qué dice, quién lo causó y a qué cosa se refiere.
/// </summary>
/// <param name="Kind">Uno de <see cref="NotificationCatalog"/>.</param>
/// <param name="ActorId">Quien hizo lo que se avisa. No recibe su propio aviso.</param>
/// <param name="EntityType">«Task», «Ticket», «Project»…: a dónde lleva el aviso.</param>
public sealed record NotificationMessage(
    Guid TenantId,
    string Kind,
    string Subject,
    string Body,
    Guid? ActorId = null,
    string? EntityType = null,
    Guid? EntityId = null);

/// <summary>
/// Manda avisos. Es la única puerta: decide quién los recibe de verdad y los entrega.
/// </summary>
public interface INotificationSender
{
    /// <summary>Lo manda a quien corresponda de la lista y devuelve a cuántos les llegó.</summary>
    Task<int> SendAsync(NotificationMessage message, IEnumerable<Guid> recipients, CancellationToken ct = default);
}

/// <summary>
/// Empuja un aviso recién creado a la pantalla de quien lo recibe. Lo implementa el host, que es
/// quien tiene el hub de SignalR.
/// </summary>
public interface INotificationPush
{
    Task PushAsync(Guid userId, NotificationDto notification, CancellationToken ct = default);
}

/// <summary>
/// Responde <see cref="INotificationSender"/>.
///
/// Por orden, y cada paso por un motivo:
/// <list type="number">
/// <item><b>Quita a quien lo causó.</b> Nadie quiere un aviso de lo que acaba de hacer.</item>
/// <item><b>Quita a quien no es de la organización</b> (<see cref="IUserDirectory"/>). Un
/// comentario puede mencionar un identificador inventado, y un aviso a él no es de nadie.</item>
/// <item><b>Aplica las preferencias de cada uno</b>, con su rol: los avisos de administración no
/// llegan a quien no administra, y las horas de silencio se respetan.</item>
/// <item><b>Guarda y empuja.</b> El aviso queda en la lista de la persona y, si tiene la aplicación
/// abierta, le aparece en el momento.</item>
/// </list>
///
/// Antes cada sitio que avisaba —las exportaciones, las automatizaciones— repetía las preferencias
/// a su manera, y el resto de la aplicación no avisaba de nada.
/// </summary>
public sealed class NotificationSender(
    TimeProvider timeProvider,
    IUserDirectory users,
    INotificationPreferencesRepository preferences,
    INotificationRepository notifications,
    INotificationsUnitOfWork unitOfWork,
    INotificationPush push,
    ILogger<NotificationSender> logger) : INotificationSender
{
    public async Task<int> SendAsync(NotificationMessage message, IEnumerable<Guid> recipients, CancellationToken ct = default)
    {
        var candidates = recipients
            .Where(id => id != Guid.Empty && id != message.ActorId)
            .Distinct()
            .ToList();
        if (candidates.Count == 0) return 0;

        var known = await users.GetAsync(message.TenantId, candidates, ct);
        if (known.Count == 0) return 0;

        var saved = await preferences.GetForUsersAsync(message.TenantId, known.Keys.ToList(), ct);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var nowTime = TimeOnly.FromDateTime(now);

        var created = new List<Notification>();
        foreach (var user in known.Values)
        {
            var theirs = saved.GetValueOrDefault(user.Id) ?? NotificationPreferences.CreateDefault(message.TenantId, user.Id);
            if (!theirs.ShouldDeliver(message.Kind, nowTime, user.IsAdmin)) continue;

            var result = Notification.Create(now, message.TenantId, user.Id, "InApp", message.Subject, message.Body,
                message.ActorId, message.Kind, message.EntityType, message.EntityId);

            if (result.IsFailure)
            {
                logger.LogWarning("No se pudo crear el aviso {Kind} para {User}: {Error}", message.Kind, user.Id, result.Error);
                continue;
            }

            await notifications.AddAsync(result.Value!, ct);
            created.Add(result.Value!);
        }

        if (created.Count == 0) return 0;
        await unitOfWork.SaveChangesAsync(ct);

        foreach (var notification in created)
        {
            // Que la pantalla no reciba el empujón no pierde el aviso: ya está guardado y sale en su
            // lista. Por eso un fallo aquí se anota y no se propaga.
            try { await push.PushAsync(notification.RecipientUserId, NotificationDto.FromEntity(notification), ct); }
            catch (Exception ex) { logger.LogWarning(ex, "No se pudo empujar el aviso {Id}", notification.Id); }
        }

        return created.Count;
    }
}
