using System.Collections.Concurrent;

namespace GameServer;

public interface ISession
{
    void NextPlayer();
    bool JoinSession(Guid id, Player new_player);
    public ConcurrentDictionary<Guid, Player> GetPlayers();
    public Player GetCurrentPlayer();
} 