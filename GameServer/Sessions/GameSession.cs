using System.Collections.Concurrent;

namespace GameServer;

public class GameSession(ILogger<GameSession> logger) : ISession
{
    public string Code { get; } = Guid.NewGuid().ToString("N")[..6].ToUpper();
    public ConcurrentDictionary<Guid, Player> Players { get; } = new();
    public int CurrentPlayerIndex { get; private set; } = 0;
    public Player CurrentPlayer => Players.ElementAt(CurrentPlayerIndex).Value;

    public bool JoinSession(Guid id, Player new_player)
    {
        if (!Players.TryAdd(id, new_player))
        {
            logger.LogWarning(
                $"Failed to add new player to session {Code}: player {new_player.Username} with id {new_player.Id} already exists"
                );
            return false;
        }
        return true;
    }

    public void NextPlayer()
    {
        if (Players.Count == 0) return;
        CurrentPlayerIndex = (CurrentPlayerIndex + 1) % Players.Count;
    }

    public ConcurrentDictionary<Guid, Player> GetPlayers() => Players;
    public Player GetCurrentPlayer() => CurrentPlayer;
}