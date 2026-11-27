using System.Collections.Concurrent;

namespace GameServer;

public class ConnectionManager
{
    private readonly ConcurrentDictionary<string, Guid> _connectionToUser = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _disconnectTimers = new();
    private readonly ConcurrentDictionary<Guid, int> _userConnectionCount = new();

    
    public void AddConnection(string connectionId, Guid userId)
    {
        _connectionToUser[connectionId] = userId;
        if (_connectionToUser.TryAdd(connectionId, userId))
        {
            _userConnectionCount.AddOrUpdate(userId, 1, (key, oldValue) => oldValue + 1);
        }
        if (_disconnectTimers.TryRemove(userId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    public Guid? RemoveConnection(string connectionId)
    {
        if (_connectionToUser.TryRemove(connectionId, out var userId))
        {
            _userConnectionCount.AddOrUpdate(userId, 0, (key, oldValue) => Math.Max(0, oldValue - 1));
            if (_userConnectionCount.TryGetValue(userId, out var count) && count == 0)
            {
                _userConnectionCount.TryRemove(userId, out _);
            }
            return userId;
        }
        return null;
    }

    public Guid? GetUserId(string connectionId)
    {
        return _connectionToUser.TryGetValue(connectionId, out var userId) ? userId : null;
    }

    public void SetDisconnectTimer(Guid userId, CancellationTokenSource cts)
    {
        _disconnectTimers[userId] = cts;
    }
    public bool IsOnline(Guid userId)
    {
        return _userConnectionCount.ContainsKey(userId);
    }
}