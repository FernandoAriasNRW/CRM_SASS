using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Communication.Presentation.Hubs;

/// <summary>
/// Los mensajes nuevos de un canal.
///
/// No pedía autenticación —los otros dos hubs sí— y el grupo era el canal que dijera el cliente:
/// cualquiera, con o sin sesión, podía escuchar el canal de otra organización. Ahora exige token y
/// el inquilino del grupo sale de él. Ver <see cref="RealtimeGroups"/>.
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    public async Task JoinChannel(Guid channelId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Channel(CallerTenant(), channelId));
    }

    public async Task LeaveChannel(Guid channelId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroups.Channel(CallerTenant(), channelId));
    }

    private Guid CallerTenant()
    {
        var tenantId = UserClaims.TenantId(Context.User);
        return tenantId != Guid.Empty ? tenantId : throw new HubException("El token no lleva inquilino.");
    }
}
