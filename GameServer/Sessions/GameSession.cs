using System.Collections.Concurrent;

namespace GameServer;

public class GameSession(ILogger<GameSession> logger) : ISession
{
    public string Code { get; } = Guid.NewGuid().ToString("N")[..6].ToUpper();
    public ConcurrentDictionary<Guid, Player> Players { get; } = new();
    public int CurrentPlayerIndex { get; private set; } = 0;
    private readonly List<Guid> _turnOrder = new();
    public Player? CurrentPlayer
    {
        get
        {
            if (_turnOrder.Count == 0) return null;

            var currentGuid = _turnOrder[CurrentPlayerIndex % _turnOrder.Count];
            return Players.TryGetValue(currentGuid, out var player) ? player : null;
        }
    }

    public bool JoinSession(Player new_player)
    {
        if (Players.Values.Any(p => p.Username.Equals(new_player.Username, StringComparison.OrdinalIgnoreCase)))
        {
            logger.LogWarning(
                $"Failed to add new player to session {Code}: player {new_player.Username} with username {new_player.Username} already exists"
                );
            return false;
        }
        
        if (!Players.TryAdd(new_player.Id, new_player))
        {
            logger.LogWarning(
                $"Failed to add new player to session {Code}: player {new_player.Username} with id {new_player.Id} already exists"
                );
            return false;
        }

        _turnOrder.Add(new_player.Id);
        return true;
    }

    public bool LeaveSession(Guid id)
    {
        var idx = _turnOrder.IndexOf(id);
        if (idx < 0 || !Players.TryRemove(id, out var _))
        {
            logger.LogWarning(
                $"Failed to remove player from session {Code}: player with id {id} doesn`t exists"
                );
            return false;
        }

        _turnOrder.Remove(id);

        if (_turnOrder.Count == 0)
        {
            CurrentPlayerIndex = 0;
            return true;
        }

        if (idx < CurrentPlayerIndex)
            CurrentPlayerIndex--;

        else if (CurrentPlayerIndex >= _turnOrder.Count)
            CurrentPlayerIndex = 0;

        return true;
    }

    public void NextPlayer()
    {
        if (Players.Count == 0) return;
        CurrentPlayerIndex = (CurrentPlayerIndex + 1) % _turnOrder.Count;
    }

    public ConcurrentDictionary<Guid, Player> GetPlayers() => Players;
    public Player? GetCurrentPlayer() => CurrentPlayer;
    public string GetCode() => Code;

}