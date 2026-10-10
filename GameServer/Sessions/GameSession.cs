using System.Collections.Concurrent;
//using GameDomain;

namespace GameServer;

public class GameSession(ILogger<GameSession> logger, GameState gameState) : Session
{
    public string Code { get; } = Guid.NewGuid().ToString("N")[..6].ToUpper();

    public bool JoinSession(Player new_player)
    {
        if (gameState.Players.Any(p => p.Name.Equals(new_player.Name, StringComparison.OrdinalIgnoreCase)))
        {
            logger.LogWarning(
                $"Failed to add new player to session {Code}: player {new_player.Name} with username {new_player.Name} already exists"
                );
            return false;
        }
        
        if (gameState.Players.Any(p => p.Id == new_player.Id))
        {
            logger.LogWarning(
                $"Failed to add new player to session {Code}: player {new_player.Name} with id {new_player.Id} already exists"
                );
            return false;
        }

        gameState.Players.Add(new_player);
        return true;
    }

    public bool LeaveSession(Guid id)
    {
        if (!gameState.Players.Any(p => p.Id == id))
        {
            logger.LogWarning(
                $"Failed to remove player from session {Code}: player with id {id} doesn`t exists"
                );
            return false;
        }

        gameState.Players.RemoveAll(p => p.Id == id);

        if (gameState.Players.Count == 0)
        {
            gameState.CurrentPlayerId = null;
            return true;
        }

        return true;
    }

    public void NextPlayer()
    {
        if (gameState.Players.Count == 0) return;
        var index = gameState.Players.FindIndex(p => p.Id == gameState.CurrentPlayerId);
        gameState.CurrentPlayerId = gameState.Players[(index + 1) % gameState.Players.Count].Id;
    }

    public void NextPhase()
    {
        gameState.Phase = gameState.Phase switch
        {
            TurnPhase.Roll => TurnPhase.Trade,
            TurnPhase.Trade => TurnPhase.Actions,
            TurnPhase.Actions => TurnPhase.Roll,
            _ => throw new InvalidOperationException($"Invalid game phase: {gameState.Phase}")
        };
    }

    public int RollDice()
    {
        if (gameState.Phase != TurnPhase.Roll)
        {
            logger.LogWarning($"Cannot roll dice in phase {gameState.Phase}");
            return -1;
        }

        var diceRoll = new Random().Next(1, 6) + new Random().Next(1, 6);
        logger.LogInformation($"Player {gameState.CurrentPlayerId} rolled a {diceRoll}");
        return diceRoll;
    }

    public Player[] GetPlayers() => gameState.Players.ToArray();
    public GameState GetGameState() => gameState;
}