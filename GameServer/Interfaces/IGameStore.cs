using System.Collections.Concurrent;

namespace GameServer;

public interface IGameStore
{
    public ConcurrentDictionary<string, ISession> GetSessions();
    ISession? GetSession(string sessionId);
    bool AddSession(string sessionId, ISession session);
}