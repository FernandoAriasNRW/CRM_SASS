using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace WorkItems.Presentation.Hubs;

/// <summary>
/// Las tareas que se mueven en el tablero de un proyecto.
///
/// El cliente elige el proyecto y el inquilino lo pone el token, así que pedir el proyecto de
/// otra organización no da acceso a nada. Ver <see cref="RealtimeGroups"/>.
/// </summary>
[Authorize]
public class BoardHub : Hub
{
    /// <summary>Todas las tareas de la organización de quien llama. Ver <see cref="RealtimeGroups.Tasks"/>.</summary>
    public async Task JoinTasks()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Tasks(CallerTenant()));
    }

    public async Task LeaveTasks()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroups.Tasks(CallerTenant()));
    }

    public async Task JoinBoard(Guid projectId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Board(CallerTenant(), projectId));
    }

    public async Task LeaveBoard(Guid projectId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroups.Board(CallerTenant(), projectId));
    }

    private Guid CallerTenant()
    {
        var tenantId = UserClaims.TenantId(Context.User);
        return tenantId != Guid.Empty ? tenantId : throw new HubException("El token no lleva inquilino.");
    }
}
