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
    public List<PendingDecision> PendingDecisions { get; set; } = new();
    public Guid[] SetupPlayerOrder { get; set; } = Array.Empty<Guid>();
    public int SetupPlacementsCompleted { get; set; }

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

    public int CalculateVictoryPoints(Guid playerId)
    {
        // The diagram does not specify the scoring rules.
        throw new NotImplementedException();
    }

    public Player? GetPlayer(Guid playerId) =>
        Players.FirstOrDefault(player => player.Id == playerId);
}
