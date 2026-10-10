using System.Collections.Concurrent;

namespace GameServer;



public class GameStore(ILogger<GameStore> logger, ILogger<GameSession> sessionLogger) : IGameStore
{
    public ConcurrentDictionary<string, GameSession> Sessions { get; private set; } = new();

    public GameSession? GetSession(string code)
    {
        if (Sessions.TryGetValue(code, out var session))
            return session;

        logger.LogWarning($"Session with ID {code} not found.");
        return null;
    }

    public ConcurrentDictionary<string, GameSession> GetSessions() => Sessions;

    public bool AddSession(string code, GameSession new_session)
    {
        if (!Sessions.TryAdd(code, new_session))
        {
            logger.LogWarning($"Session with ID {code} already exists");
            return false;
        }
        return true;
    }

    public bool RemoveSession(string code)
    {
        if (!Sessions.TryRemove(code, out var _))
        {
            logger.LogWarning($"Session with ID {code} doesn`t exist");
            return false;
        }
        return true;
    }

    public GameSession CreateSession()
    {
        var session = new GameSession(sessionLogger, new GameState());
        Sessions.TryAdd(session.Code, session);
        return session;
    }
}