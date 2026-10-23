namespace GameServer;

public class GameService : IGameService
{
    private readonly ILogger<GameService> _logger;
    private readonly IPlayerService _playerService;
    private readonly IBoardService _boardService;

    public GameService(ILogger<GameService> logger, IPlayerService playerService, IBoardService boardService)
    {
        _logger = logger;
        _playerService = playerService;
        _boardService = boardService;
    }

    public bool StartGame(GameSession session)
    {
        if (session == null)
        {
            _logger.LogError("Failed to start game: session is null");
            return false;
        }
        var result = session.InitializeSetup();
        if (!result)
        {
            _logger.LogError($"Failed to start game for session {session.Code}: initialization failed");
            return false;
        }

        _boardService.SetupBoard(session, session.GetGameState().Players.Count);

        return true;
    }

    public void OnSevenRolled(GameSession session)
    {
        var state = session.GetGameState();

        var decisions = new List<PendingDecision>();

        foreach (var player in state.Players)
        {
            var totalResources = player.Resources.CountTotal();

            if (totalResources > 7)
            {
                decisions.Add(new DiscardResourcesDecision
                {
                    PlayerId = player.Id,
                    RequiredCount = totalResources / 2
                });
            }
        }

        state.PendingDecisions.AddRange(decisions);
    }

    public bool SubmitDiscardResources(
        GameSession session,
        Guid playerId,
        Dictionary<ResourceType, int> selectedResources)
    {
        var state = session.GetGameState();

        var decision = state.PendingDecisions
            .OfType<DiscardResourcesDecision>()
            .FirstOrDefault(d => d.PlayerId == playerId);

        if (decision == null)
        {
            _logger.LogWarning($"Player {playerId} attempted to discard resources without a pending decision.");
            return false;
        }

        var selectedCount = selectedResources.Values.Sum();

        if (selectedCount != decision.RequiredCount)
        {
            _logger.LogWarning($"Player {playerId} attempted to discard {selectedCount} resources, but {decision.RequiredCount} are required.");
            return false;
        }

        _playerService.SpendResources(
            session,
            playerId,
            selectedResources);

        state.PendingDecisions.Remove(decision);

        return true;
    }

    public bool SubmitInitialPlacement(GameSession session, Guid playerId, AxialCoords nodeCoords)
    {
        lock (session.Gate)
        {
            var state = session.GetGameState();
            if (state.Status != GameStatus.Setup || state.CurrentPlayerId != playerId)
            {
                _logger.LogWarning($"Initial placement rejected in session {session.Code} for player {playerId}: " +
                                    $"status is {state.Status}, current player is {state.CurrentPlayerId}.");
                return false;
            }
            if (!_boardService.PlaceInitialCamp(session, playerId, nodeCoords))
            {
                _logger.LogWarning($"Initial placement rejected in session {session.Code} for player {playerId}: " +
                                   $"cannot place a camp at {nodeCoords}");
                return false;
            }
            state.SetupPlacementsCompleted++;
            var count = state.SetupPlayerOrder.Length;
            if (state.SetupPlacementsCompleted == count * GameConstants.InitialCampsCount)
            {
                state.Status = GameStatus.Playing;
                state.Phase = TurnPhase.Roll;
                state.TurnNumber = 1;
                state.CurrentPlayerId = state.SetupPlayerOrder[0];
                return true;
            }

            var round = state.SetupPlacementsCompleted / count;
            var offset = state.SetupPlacementsCompleted % count;
            var index = round == 1 ? count - 1 - offset : offset;
            state.CurrentPlayerId = state.SetupPlayerOrder[index];

            return true;
        }
    }
}
