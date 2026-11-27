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
        lock (session.Gate)
        {
            if (session.GetGameState().PendingExplorerMove != null)
                return false;

            var result = session.InitializeSetup();
            if (!result)
            {
                _logger.LogError($"Failed to start game for session {session.Code}: initialization failed");
                return false;
            }

            _boardService.SetupBoard(session, session.GetGameState().Players.Count);

            return true;
        }
    }

    public void OnSevenRolled(GameSession session)
    {
        lock (session.Gate)
        {
            var state = session.GetGameState();
            if (state.PendingExplorerMove != null)
                throw new GameRuleException("Pending Explorer move not finished.");

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
    }

    public bool SubmitDiscardResources(
        GameSession session,
        Guid playerId,
        Dictionary<ResourceType, int> selectedResources)
    {
        lock (session.Gate)
        {
            var state = session.GetGameState();
            if (state.PendingExplorerMove != null)
                return false;

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

    public bool SubmitInitialPlacement(GameSession session, Guid playerId, AxialCoords nodeCoords)
    {
        lock (session.Gate)
        {
            var state = session.GetGameState();
            if (state.PendingExplorerMove != null || state.Status != GameStatus.Setup || state.CurrentPlayerId != playerId)
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
            state.CompletedSetupPlacementsCount++;
            var count = state.SetupPlayerOrder.Length;
            if (state.CompletedSetupPlacementsCount == count * GameConstants.InitialCampsCount)
            {
                state.Status = GameStatus.Playing;
                state.Phase = TurnPhase.Roll;
                state.TurnNumber = 1;
                state.CurrentPlayerId = state.SetupPlayerOrder[0];
                return true;
            }

            var round = state.CompletedSetupPlacementsCount / count;
            var offset = state.CompletedSetupPlacementsCount % count;
            var index = round == 1 ? count - 1 - offset : offset;
            state.CurrentPlayerId = state.SetupPlayerOrder[index];

            return true;
        }
    }

    public void ConvertExplorerToCamp(GameSession session, Guid playerId, AxialCoords nodeCoords)
    {
        lock (session.Gate)
        {
            var state = session.GetGameState();
            var player = state.GetPlayer(playerId);
            if (player == null)
            {
                _logger.LogWarning($"Player with ID {playerId} not found.");
                throw new GameRuleException("Player not found.");
            }

            if (state.PendingExplorerMove != null)
            {
                _logger.LogWarning($"Player with ID {playerId} tried convert explorer while moving pending.");
                throw new GameRuleException("Pending Explorer move not finished.");
            }

            if (!_playerService.CanBuildCamp(player))
            {
                _logger.LogWarning($"Player with ID {playerId} cannot afford to build a camp.");
                throw new GameRuleException("The player cannot afford to build a camp.");
            }

            var tribe = _boardService.PlaceCamp(session, playerId, nodeCoords);

            player.TribesCollected.Add(tribe);
            _playerService.SpendResources(session, playerId, GameCosts.CampCost);

            _logger.LogInformation(
                $"Converted explorer to camp for player {playerId} at ({nodeCoords.q}, {nodeCoords.r}).");
        }
    }

    public void CreateExplorer(GameSession session, Guid playerId, AxialCoords nodeCoords)
    {
        lock (session.Gate)
        {
            var state = session.GetGameState();
            var player = state.GetPlayer(playerId);
            if (player == null)
            {
                _logger.LogWarning($"Player with ID {playerId} not found.");
                throw new GameRuleException("Player not found.");
            }

            if (state.PendingExplorerMove != null)
            {
                _logger.LogWarning($"Player with ID {playerId} tried convert explorer while moving pending.");
                throw new GameRuleException("Pending Explorer move not finished.");
            }

            if (!_playerService.CanCreateExplorer(player))
            {
                _logger.LogWarning($"Player with ID {playerId} cannot afford to build an explorer.");
                throw new GameRuleException("Player cannot afford to build an explorer.");
            }

            _boardService.PlaceExplorer(session, playerId, nodeCoords);
            _playerService.SpendResources(session, playerId, GameCosts.ExplorerCost);
        }
    }

    public List<AxialCoords> MoveExplorer(GameSession session, Guid playerId, List<AxialCoords> route)
    {
        lock (session.Gate)
        {
            var state = session.GetGameState();
            var player = state.GetPlayer(playerId);
            if (player == null)
            {
                _logger.LogWarning($"Player with ID {playerId} not found.");
                throw new GameRuleException("Player not found.");
            }

            if (state.PendingExplorerMove != null)
            {
                _logger.LogWarning(
                    $"Player with ID {playerId} tried to move explorer, " +
                    $"but GameState have pending move for player {state.PendingExplorerMove.PlayerId}"
                );
                throw new InvalidOperationException("Game has already pending move.");

            }

            if (route == null || route.Count < 2 || route.Any(coords => coords == null))
            {
                _logger.LogWarning($"Player with ID {playerId} provided an invalid explorer route.");
                throw new GameRuleException("The route must contain at least one edge.");
            }

            var cost = _playerService.CalculateMoveCost(player, route.Count - 1);

            if (!_playerService.CanAfford(player, cost))
            {
                _logger.LogWarning($"Player with ID {playerId} cannot afford to move an explorer.");
                throw new GameRuleException("Player cannot afford to move an explorer.");
            }

            var (clothingReq, shelterReq) = _boardService.CheckRoute(session, playerId, route);

            if (!_playerService.MeetsTravelRequirements(player, clothingReq, shelterReq))
            {
                _logger.LogWarning($"Player with ID {playerId} does not meet the route requirements.");
                throw new GameRuleException("The player does not meet the path requirements.");
            }

            var move = new ExplorerMoveState
            {
                PlayerId = playerId,
                Route = new List<AxialCoords>(route),
                CurPosInRoute = 0,
            };
            _playerService.SpendResources(session, playerId, cost);
            state.Board.ExplorerPositions.Remove(move.Position);
            state.PendingExplorerMove = move;

            return ContinueMoving(session) ?? throw new InvalidOperationException("Failed to move explorer.");
        }
    }

    private List<AxialCoords>? ContinueMoving(GameSession session)
    {
        var state = session.GetGameState();

        if (state.PendingExplorerMove == null)
        {
            _logger.LogWarning(
                $"Attempt to continue moving explorer, " +
                $"but GameState has no pending move to continue"
            );
            throw new InvalidOperationException("Game has no pending move to continue.");
        }

        var move = state.PendingExplorerMove;
        var route = move.Route;
        var startPos = move.CurPosInRoute;

        if (startPos < 0 || startPos >= route.Count)
        {
            _logger.LogWarning(
                $"Ivalid pending move state for player {state.PendingExplorerMove.PlayerId}: " +
                "current position greater route size."
            );
            throw new InvalidOperationException("Invalid pending move state.");
        }

        if (move.ActiveCounter != null)
            return null;

        while (move.CurPosInRoute < route.Count - 1)
        {
            var from = move.Position;
            var to = route[move.CurPosInRoute + 1];
            move.ActiveCounter = _boardService.TakeExplorationCounter(session, from, to);
            move.CurPosInRoute++;

            if (move.ActiveCounter != null)
            {
                ProcessExplorationCounter(session);
                if (move.ActiveCounter != null)
                    return route[startPos..(move.CurPosInRoute + 1)];
            }
        }

        if (state.Board.IsOccupied(move.Position))
            throw new GameRuleException("Target node for move explorer is occupied.");

        state.Board.ExplorerPositions.Add(move.Position, move.PlayerId);
        state.PendingExplorerMove = null;

        return move.CurPosInRoute == startPos
            ? null
            : route[startPos..(move.CurPosInRoute + 1)];
    }

    public bool SubmitChooseEnemy(GameSession session, Guid playerId, Guid decisionId, EnemyType enemyType)
    {
        lock (session.Gate)
        {
            var state = session.GetGameState();
            var decision = GetExplorationDecision<ChooseEnemyDecision>(state, playerId, decisionId);

            if (!Enum.IsDefined(enemyType))
                return false;

            state.PendingDecisions.Remove(decision);
            state.PendingDecisions.Add(new ChooseEnemyDestinationDecision
            {
                PlayerId = playerId,
                ExplorationCounterId = decision.ExplorationCounterId,
                EnemyType = enemyType
            });
            return true;
        }
    }

    public (bool isSuccess, List<AxialCoords>? moveResult) SubmitChooseEnemyDestination(
        GameSession session,
        Guid playerId,
        Guid decisionId,
        AxialCoords? destination
    )
    {
        lock (session.Gate)
        {
            var state = session.GetGameState();
            var decision = GetExplorationDecision<ChooseEnemyDestinationDecision>(state, playerId, decisionId);
            if (!_boardService.TryMoveEnemy(session, decision.EnemyType, destination))
            {
                _logger.LogWarning("Enemy move rejected.");
                return (false, null);
            }

            var eligiblePlayerIds = GetEligibleStealTargets(session, playerId, destination!);

            state.PendingDecisions.Remove(decision);
            if (eligiblePlayerIds.Count != 0)
            {
                state.PendingDecisions.Add(new ChooseStealTargetDecision
                {
                    PlayerId = playerId,
                    ExplorationCounterId = decision.ExplorationCounterId,
                    EnemyType = decision.EnemyType,
                    Destination = destination!,
                    EligiblePlayerIds = eligiblePlayerIds
                });
                return (true, null);
            }
            CompleteExplorationCounter(state);
            return (true, ContinueMoving(session));
        }
    }

    public (bool isSuccess, List<AxialCoords>? moveResult) SubmitChooseStealTarget(
        GameSession session,
        Guid playerId,
        Guid decisionId,
        Guid? targetPlayerId
    )
    {
        lock (session.Gate)
        {
            var state = session.GetGameState();
            var decision = GetExplorationDecision<ChooseStealTargetDecision>(state, playerId, decisionId);
            if (!_boardService.IsEnemyAt(session, decision.EnemyType, decision.Destination))
            {
                _logger.LogWarning($"Not found {decision.EnemyType} on {decision.Destination}");
                return (false, null);
            }
            if (targetPlayerId is Guid targetId)
            {
                if (!decision.EligiblePlayerIds.Contains(targetId))
                    return (false, null);

                var target = state.GetPlayer(targetId)!;
                var player = state.GetPlayer(playerId)!;
                var resources = target.Resources.Resources.Where(entry => entry.Value > 0).ToArray();
                var total = resources.Sum(entry => (long)entry.Value);
                var ticket = Random.Shared.NextInt64(total);
                foreach (var (resource, count) in resources)
                {
                    if (ticket < count)
                    {
                        target.Resources.Spend(resource, 1);
                        player.Resources.Add(resource, 1);
                        break;
                    }
                    ticket -= count;
                }
            }

            state.PendingDecisions.Remove(decision);
            CompleteExplorationCounter(state);
            return (true, ContinueMoving(session));
        }
    }

    public (bool isSuccess, List<AxialCoords>? moveResult) SubmitChooseDesertificationHex(
        GameSession session,
        Guid playerId,
        Guid decisionId,
        AxialCoords? hexCoords
    )
    {
        lock (session.Gate)
        {
            var state = session.GetGameState();
            var decision = GetExplorationDecision<ChooseDesertificationHexDecision>(state, playerId, decisionId);

            if (!_boardService.TryDesertifyHex(session, decision.Terrain, hexCoords))
                return (false, null);

            state.PendingDecisions.Remove(decision);
            CompleteExplorationCounter(state);
            return (true, ContinueMoving(session));
        }
    }

    private List<Guid> GetEligibleStealTargets(GameSession session, Guid playerId, AxialCoords destination)
    {
        var state = session.GetGameState();
        var neighboringOwners = _boardService.GetAdjacentCampOwnerIds(session, destination);

        return state.Players
            .Where(player => player.Id != playerId && neighboringOwners.Contains(player.Id) && player.Resources.CountTotal() > 0)
            .Select(player => player.Id)
            .Distinct()
            .ToList();
    }

    //return true if required decision
    private bool ProcessExplorationCounter(GameSession session)
    {
        var state = session.GetGameState();
        var counter = state.PendingExplorerMove!.ActiveCounter
            ?? throw new InvalidOperationException("Active counter for process must be not null.");

        switch (counter)
        {
            case EnemyExplorationCounter enemyCounter:
                ProcessEnemyCounter(state, enemyCounter);
                return true;
            case DesertificationCounter desertificationCounter:
                return ProcessDesertificationCounter(session, desertificationCounter);
            case AdaptationCounter adaptationCounter:
                ProcessAdaptationCounter(state, adaptationCounter);
                return false;
            default:
                _logger.LogWarning($"Unknown exploration counter type: {counter}");
                throw new InvalidOperationException("Unknown exploration counter type.");
        }
    }

    private void ProcessEnemyCounter(GameState state, EnemyExplorationCounter counter)
    {
        var playerId = state.PendingExplorerMove!.PlayerId;

        state.PendingDecisions.Add(new ChooseEnemyDecision
        {
            PlayerId = playerId,
            ExplorationCounterId = counter.Id
        });
    }

    private bool ProcessDesertificationCounter(GameSession session, DesertificationCounter counter)
    {
        var state = session.GetGameState();
        var playerId = state.PendingExplorerMove!.PlayerId;

        var hasEligibleHex = _boardService.HasDesertificationTarget(session, counter.Terrain);

        if (!hasEligibleHex)
        {
            CompleteExplorationCounter(state);
            return false;
        }

        state.PendingDecisions.Add(new ChooseDesertificationHexDecision
        {
            PlayerId = playerId,
            ExplorationCounterId = counter.Id,
            Terrain = counter.Terrain
        });
        return true;
    }

    private void ProcessAdaptationCounter(GameState state, AdaptationCounter counter)
    {
        var playerId = state.PendingExplorerMove!.PlayerId;

        state.Victory.AdaptationCardOwners[counter.Color] = playerId;
        CompleteExplorationCounter(state);
    }

    private void CompleteExplorationCounter(GameState state)
    {
        var counter = state.PendingExplorerMove!.ActiveCounter
                ?? throw new InvalidOperationException("Active counter for complete must be not null.");

        var playerId = state.PendingExplorerMove!.PlayerId;
        var player = state.GetPlayer(playerId)
            ?? throw new InvalidOperationException("Moving player not found.");
        player.ExplorationCounters.Add(counter);

        state.PendingExplorerMove!.ActiveCounter = null;

        UpdateExplorationCardOwner(state, player);
    }

    private void UpdateExplorationCardOwner(GameState state, Player player)
    {
        if (player.ExplorationCounters.Count < GameConstants.MinCountersForExplorationCart)
            return;

        var owner = state.Victory.ExplorationCardOwnerId is Guid ownerId ? state.GetPlayer(ownerId) : null;
        if (owner == null || player.ExplorationCounters.Count > owner.ExplorationCounters.Count)
            state.Victory.ExplorationCardOwnerId = player.Id;
        _logger.LogInformation($"Exploration Card owner updated: old({owner}) new({player.Id})");
    }

    private TDecision GetExplorationDecision<TDecision>(
        GameState state, Guid playerId, Guid decisionId
    )
        where TDecision : ExplorationDecision
    {
        var decision = state.PendingDecisions.FirstOrDefault();
        if (decision == null || decision.Id != decisionId)
        {
            _logger.LogWarning($"Decision with id {decisionId} null or not found");
            throw new InvalidOperationException("Not found decision.");
        }

        if (decision.PlayerId != playerId)
        {
            _logger.LogWarning($"Player {playerId} tried submit decision for player {decision.PlayerId}");
            throw new InvalidOperationException("Attempt to make a decision for someone else");
        }

        if (decision is not TDecision expDecision)
        {
            _logger.LogWarning($"Player {playerId} tried submit decision that wasn't pended");
            throw new InvalidOperationException("Decision wasn't pended");
        }

        var move = state.PendingExplorerMove;
        if (move == null || move.PlayerId != playerId || state.GetPlayer(playerId) == null ||
            move.ActiveCounter == null || move.ActiveCounter.Id != expDecision.ExplorationCounterId)
            throw new InvalidOperationException("Decision does not belong to the active explorer move.");

        var matchesCounter = expDecision switch
        {
            ChooseEnemyDecision or ChooseEnemyDestinationDecision or ChooseStealTargetDecision
                => move.ActiveCounter is EnemyExplorationCounter,
            ChooseDesertificationHexDecision desertification
                => move.ActiveCounter is DesertificationCounter counter && counter.Terrain == desertification.Terrain,
            _ => false
        };
        if (!matchesCounter)
            throw new InvalidOperationException("Decision does not match the active exploration counter.");

        return expDecision;
    }
}

