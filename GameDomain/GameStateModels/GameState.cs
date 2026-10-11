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
    public List<Player> Players { get; set; } = new();
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

    public int CalculateVictoryPoints(Guid playerId)
    {
        // The diagram does not specify the scoring rules.
        throw new NotImplementedException();
    }

    public Player? GetPlayer(Guid playerId) =>
        Players.FirstOrDefault(player => player.Id == playerId);
}
