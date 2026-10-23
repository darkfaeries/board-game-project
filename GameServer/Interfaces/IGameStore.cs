using System.Collections.Concurrent;

namespace GameServer;

public interface IGameStore
{
    public ConcurrentDictionary<string, GameSession> GetSessions();
    GameSession? GetSession(string code);
    bool AddSession(string code, GameSession session);
    bool RemoveSession(string code);
    GameSession CreateSession();
}