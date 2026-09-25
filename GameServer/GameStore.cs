using System.Collections.Concurrent;
using System.Net.WebSockets;

public class GameSession
{
    public string Code { get; } = Guid.NewGuid().ToString("N")[..6].ToUpper();
    public ConcurrentDictionary<Guid, WebSocket> Players { get; } = new();
}

public class GameStore
{
    public ConcurrentDictionary<string, GameSession> Sessions { get; } = new();
}