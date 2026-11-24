using System.Collections.Concurrent;

namespace GameServer;

public class ConnectionManager
{
    private readonly ConcurrentDictionary<string, Guid> _connectionToUser = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _disconnectTimers = new();

    public void AddConnection(string connectionId, Guid userId)
    {
        _connectionToUser[connectionId] = userId;
        
        if (_disconnectTimers.TryRemove(userId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    public Guid? RemoveConnection(string connectionId)
    {
        _connectionToUser.TryRemove(connectionId, out var userId);
        return userId == Guid.Empty ? null : userId;
    }

    public Guid? GetUserId(string connectionId)
    {
        return _connectionToUser.TryGetValue(connectionId, out var userId) ? userId : null;
    }

    public void SetDisconnectTimer(Guid userId, CancellationTokenSource cts)
    {
        _disconnectTimers[userId] = cts;
    }
}