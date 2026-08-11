using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace WorkItems.Presentation.Hubs;

[Authorize]
public class BoardHub : Hub
{
    public async Task JoinBoard(string projectId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, projectId);
    }
    
    public async Task LeaveBoard(string projectId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, projectId);
    }
}
