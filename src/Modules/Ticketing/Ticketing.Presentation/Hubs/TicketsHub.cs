using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace Ticketing.Presentation.Hubs;

[Authorize]
public class TicketsHub : Hub
{
    public async Task JoinTickets(string tenantId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, tenantId);
    }
    
    public async Task LeaveTickets(string tenantId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, tenantId);
    }
}
