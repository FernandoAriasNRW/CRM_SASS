using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Ticketing.Presentation.Hubs;

/// <summary>
/// Los cambios de estado de los tickets de la organización.
///
/// <c>JoinTickets</c> recibía el inquilino del cliente y metía la conexión en ese grupo: con el
/// identificador de otra organización se veía su tablero moverse. Ahora no recibe nada; el grupo
/// sale del token. Ver <see cref="RealtimeGroups"/>.
/// </summary>
[Authorize]
public class TicketsHub : Hub
{
    public async Task JoinTickets()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Tenant(CallerTenant()));
    }

    public async Task LeaveTickets()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroups.Tenant(CallerTenant()));
    }

    private Guid CallerTenant()
    {
        var tenantId = UserClaims.TenantId(Context.User);
        return tenantId != Guid.Empty ? tenantId : throw new HubException("El token no lleva inquilino.");
    }
}
