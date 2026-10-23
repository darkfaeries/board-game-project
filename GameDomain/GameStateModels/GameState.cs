#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class GameState
{
    public GameStatus Status { get; set; }
    public TurnPhase Phase { get; set; }
    public Guid? CurrentPlayerId { get; set; }
    public int TurnNumber { get; set; } = 1;
    public List<Player> Players { get; set; } = new();
    public Dictionary<PlayerColor, int> AvailableColors { get; set; } = new()
    {
        { PlayerColor.Red, 1 },
        { PlayerColor.Blue, 1 },
        { PlayerColor.White, 1 },
        { PlayerColor.Orange, 1 }
    };
    public BoardState Board { get; }
    public VictoryState Victory { get; set; } = new VictoryState();
    public PendingDecision[] PendingDecisions { get; set; } = Array.Empty<PendingDecision>();

    public GameState(BoardState board)
    {
        Board = board;
    }

    public Player GetCurrentPlayer()
    {
        if (!CurrentPlayerId.HasValue)
            throw new InvalidOperationException("Current player is not set.");

        return Players.Single(player => player.Id == CurrentPlayerId.Value);
    }

    public bool CalculateVictoryPoints(Guid playerId)
    {
        int victoryPoints = 0;
        var player = GetPlayer(playerId)!;
        victoryPoints += player.TribesCollected.Count;
        foreach (ProgressType progressType in Enum.GetValues<ProgressType>())
        {
            if (player.Progress.GetLevel(progressType) == GameConstants.MaxProgressLevel)
            {
                victoryPoints += 1;
            }
        }
        foreach (var counter in player.ExplorationCounters)
        {
            if (counter is AdaptationCounter)
            {
                victoryPoints += 1;
            }
        }

        if (Victory.ExplorationCardOwnerId == playerId)
        {
            victoryPoints += 2;
        }

        if (Victory.ExpansionCardOwnerId == playerId)
        {
            victoryPoints += 2;
        }
        
        return victoryPoints >= GameConstants.VictoryPointsToWin;
    }

    public Player? GetPlayer(Guid playerId) =>
        Players.FirstOrDefault(player => player.Id == playerId);
}
