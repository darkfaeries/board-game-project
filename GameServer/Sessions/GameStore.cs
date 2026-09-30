using System.Collections.Concurrent;

namespace GameServer;


public class GameStore(ILogger<GameStore> logger, Func<ISession> sessionFactory) : IGameStore
{
    public ConcurrentDictionary<string, ISession> Sessions { get; private set; } = new();

    public ISession? GetSession(string code)
    {
        if (Sessions.TryGetValue(code, out var session))
            return session;

        logger.LogWarning($"Session with ID {code} not found.");
        return null;
    }

    public ConcurrentDictionary<string, ISession> GetSessions() => Sessions;

    public bool AddSession(string code, ISession new_session)
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

    public ISession CreateSession()
    {
        var session = sessionFactory();
        Sessions.TryAdd(session.GetCode(), session);
        return session;
    }
}