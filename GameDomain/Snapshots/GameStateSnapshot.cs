public sealed class GameStateSnapshot
{
    public int Version { get; set; } = 1;
    public GameStatus Status { get; set; }
    public TurnPhase Phase { get; set; }
    public Guid? CurrentPlayerId { get; set; }
    public int TurnNumber { get; set; }
    public BoardSnapshot Board { get; set; } = new();
    public List<PlayerSnapshot> Players { get; set; } = new();
    public VictorySnapshot Victory { get; set; } = new();
    public List<PendingDecisionSnapshot> PendingDecisions { get; set; } = new();

    public static GameStateSnapshot FromGameState(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new GameStateSnapshot
        {
            Status = state.Status, Phase = state.Phase, CurrentPlayerId = state.CurrentPlayerId,
            TurnNumber = state.TurnNumber, Board = BoardSnapshot.FromBoardState(state.Board),
            Players = state.Players.Select(PlayerSnapshot.FromPlayer).ToList(),
            Victory = VictorySnapshot.FromVictory(state.Victory),
            PendingDecisions = state.PendingDecisions.Select(PendingDecisionSnapshot.FromDecision).ToList()
        };
    }

    public GameState ToGameState()
    {
        if (Version < 0 || !Enum.IsDefined(Status) || !Enum.IsDefined(Phase) || TurnNumber < 0 ||
            Board == null || Players == null || Victory == null || PendingDecisions == null)
            throw new InvalidOperationException("Invalid game snapshot");
        
        var board = Board.ToBoardState();
        var players = Players.Select(p => p.ToPlayer()).ToArray();
        var ids = players.Select(p => p.Id).ToHashSet();
        
        if (ids.Count != players.Length || (CurrentPlayerId.HasValue && !ids.Contains(CurrentPlayerId.Value)))
            throw new InvalidOperationException("Duplicate player or unknown current player");

        foreach (var player in players)
            foreach (var position in player.CampPositions.Concat(player.ExplorerPositions))
                if (!board.Nodes.ContainsKey(position)) throw new InvalidOperationException("Player piece is outside the board");

        var victory = Victory.ToVictory();
        var owners = victory.ArtCardOwners.Values.Concat(new[] { victory.ExplorationCardOwnerId, victory.ExpansionCardOwnerId }
            .Where(id => id.HasValue).Select(id => id!.Value));
            
        if (owners.Any(id => !ids.Contains(id))) 
            throw new InvalidOperationException("Unknown victory card owner");
        
        var decisions = PendingDecisions.Select(d => d.ToDecision()).ToArray();
        
        if (decisions.Any(d => !ids.Contains(d.PlayerId))) 
            throw new InvalidOperationException("Unknown decision owner");

        return new GameState(board)
        {
            Status = Status, Phase = Phase, CurrentPlayerId = CurrentPlayerId, TurnNumber = TurnNumber,
            Players = players, Victory = victory, PendingDecisions = decisions
        };
    }
}
