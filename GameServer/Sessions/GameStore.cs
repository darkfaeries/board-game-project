using System.Collections.Concurrent;

namespace GameServer;


public class GameStore(ILogger<GameStore> logger) : IGameStore
{
    public ConcurrentDictionary<string, ISession> Sessions { get; private set; } = new();

    public ISession? GetSession(string sessionId)
    {
        if (Sessions.TryGetValue(sessionId, out var session))
            return session;
        
        logger.LogWarning($"Session with ID {sessionId} not found.");
        return null;
    }

    public ConcurrentDictionary<string, ISession> GetSessions() => Sessions;
    public bool AddSession(string sessionId, ISession new_session)
    {
        if (!Sessions.TryAdd(sessionId, new_session))
        {
            logger.LogWarning($"Session with ID {sessionId} already exists");
            return false;
        }
        return true;
    }
}