using System.Collections.Concurrent;

namespace GameServer;

public interface IGameStore
{
    public ConcurrentDictionary<string, ISession> GetSessions();
    ISession? GetSession(string code);
    bool AddSession(string code, ISession session);
    bool RemoveSession(string code);
    ISession CreateSession();
}