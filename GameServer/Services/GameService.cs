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

    public Guid TradeWithPlayer(
        GameSession session,
        Guid sourcePlayerId,
        Guid targetPlayerId,
        Dictionary<ResourceType, int> offeredResources,
        Dictionary<ResourceType, int> requestedResources)
    {
        var state = session.GetGameState();

        var decision = new TradeDecision
        {
            SourcePlayerId = sourcePlayerId,
            TargetPlayerId = targetPlayerId,
            OfferedResources = offeredResources,
            RequestedResources = requestedResources
        };

        state.PendingDecisions.Add(decision);

        return decision.TradeId;
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

    public bool SubmitTradeDecision(
        GameSession session,
        Guid tradeId, bool isAccepted)
    {
        var state = session.GetGameState();

        var decision = state.PendingDecisions
            .OfType<TradeDecision>()
            .FirstOrDefault(d => d.TradeId == tradeId);

        if (decision == null)
        {
            _logger.LogWarning($"Player attempted to submit a trade decision without a pending decision.");
            return false;
        }

        if (!isAccepted)
        {
            state.PendingDecisions.Remove(decision);
            return true;
        }

        var tradeSuccess = _playerService.TradeWithPlayer(
            session,
            decision.SourcePlayerId,
            decision.TargetPlayerId,
            decision.OfferedResources,
            decision.RequestedResources);

        if (!tradeSuccess)
        {
            _logger.LogWarning($"Trade between player {decision.SourcePlayerId} and player {decision.TargetPlayerId} failed.");
            return false;
        }

        state.PendingDecisions.Remove(decision);
            
        return true;
    }
}