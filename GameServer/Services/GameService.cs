namespace GameServer;

public class GameService : IGameService
{
    private readonly ILogger<GameService> _logger;
    private readonly IPlayerService _playerService;

    public GameService(ILogger<GameService> logger, IPlayerService playerService)
    {
        _logger = logger;
        _playerService = playerService;
        // _boardService = boardService;
    }

    public bool StartGame(GameSession session) 
    {
        if (session == null)
        {
            _logger.LogError("Failed to start game: session is null");
            return false;
        }
        var result =  session.InitializeSetup();
        if (!result)
        {
            _logger.LogError($"Failed to start game for session {session.Code}: initialization failed");
            return false;
        }
        // boardService.SetupBoard(session); 
        // boardService.PlayersSetup(session);

        _logger.LogInformation($"Game started for session {session.Code}");
        session.GetGameState().Status = GameStatus.Playing;

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
}