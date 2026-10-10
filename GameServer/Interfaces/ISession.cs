using System.Collections.Concurrent;

namespace GameServer;

public interface ISession
{
    void NextPlayer();
    bool JoinSession(Player new_player);
    bool LeaveSession(Guid id);
    ConcurrentDictionary<Guid, Player> GetPlayers();
    Player? GetCurrentPlayer();
    string GetCode();
} 