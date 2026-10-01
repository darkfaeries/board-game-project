using Microsoft.AspNetCore.SignalR;

namespace GameServer;

class GameHub : Hub
{
    public async Task<string> CreateSession()
    {
        return "Code";
    }

    public bool JoinSession()
    {
        return true;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}