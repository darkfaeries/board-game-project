#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class GameState
{
    public GameStatus Status { get; set; }
    public TurnPhase Phase { get; set; }
    public Guid? CurrentPlayerId { get; set; }
    public int TurnNumber { get; set; }
    public Player[] Players { get; set; } = Array.Empty<Player>();
    public BoardState Board { get; } 
    public VictoryState Victory { get; set; } = new VictoryState();
    public PendingDecision[] PendingDecisions { get; set; } = Array.Empty<PendingDecision>();

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
}
