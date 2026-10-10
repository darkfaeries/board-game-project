namespace GameServer;

public class PlayerService : IPlayerService
{
    private readonly IGameStore _gameStore;
    private ILogger<PlayerService> _logger;

    public PlayerService(IGameStore gameStore, ILogger<PlayerService> logger)
    {
        _gameStore = gameStore;
        _logger = logger;
    }

    public void DistributeHexResources(string code, int diceValue)
    {
        if (diceValue == 7) return;

        var session = _gameStore.GetSession(code);
        if (session == null)
        {
            _logger.LogWarning($"Session with ID {code} not found.");
            throw new KeyNotFoundException($"Session with ID {code} not found.");
        }

        var state = session.GetGameState();

        var activeHexes = state.Board.Hexes.Values
        .Where(h => h.NumberToken == diceValue && !h.IsDesertified)
        .ToList();

        foreach (var hex in activeHexes)
        {
            var resourceType = hex.GetProducedResource();
            var adjacentNodes = state.Board.GetAdjacentIntersections(hex.Id);

            foreach (var node in adjacentNodes)
            {
                if (state.Board.IsOccupied(node.Id) && node.OccupantId.HasValue)
                {
                    var campId = node.OccupantId.Value;
                    var owner = state.Players.FirstOrDefault(p => p.CampPositions.Contains(campId));

                    owner?.Resources.Add(resourceType, 1);
                }
            }
        }
    }

    public void AddResources(string code, Guid playerId, List<ResourceType> resources)
    {
        var session = _gameStore.GetSession(code);
        var player = GetValidPlayer(session, playerId);

        foreach (var resource in resources)
        {
            player.Resources.Add(resource, 1);
        }
    }

    public void SpendResources(string code, Guid playerId, List<ResourceType> resources)
    {
        var session = _gameStore.GetSession(code);
        var player = GetValidPlayer(session, playerId);

        foreach (var resource in resources)
        {
            player.Resources.Spend(resource, 1);
        }
    }

    public (bool IsSuccess, string ErrorMessage) BuildCamp(string code, Guid playerId, Guid nodeId)
    {
        var session = _gameStore.GetSession(code);
        if (session == null) return (false, $"Session with code {code} not found.");

        var state = session.GetGameState();
        var player = state.Players.FirstOrDefault(p => p.Id == playerId);
        if (player == null) return (false, $"Player with id {playerId} not found.");

        if (!CanBuildCamp(player))
        {
            _logger.LogWarning($"Player with ID {playerId} cannot afford to build a camp.");
            return (false, $"Player with ID {playerId} cannot afford to build a camp.");
        }

        var node = state.Board.Intersections.GetValueOrDefault(nodeId);
        if (node == null)
        {
            _logger.LogWarning($"Node with ID {nodeId} not found.");
            return (false, $"Node with ID {nodeId} not found.");
        }
        if (state.Board.IsOccupied(nodeId))
        {
            _logger.LogWarning($"Node with ID {nodeId} is already occupied.");
            return (false, $"Node with ID {nodeId} is already occupied.");
        }

        SpendResources(code, playerId, GameCosts.CampCost.Keys.ToList());
        player!.CampPositions.Add(nodeId);
        return (true, "Camp built successfully.");
    }

    public (bool IsSuccess, string ErrorMessage) BuildExplorer(string code, Guid playerId, Guid nodeId)
    {
        var session = _gameStore.GetSession(code);
        if (session == null) return (false, $"Session witch code {code} not found.");

        var state = session.GetGameState();
        var player = state.Players.FirstOrDefault(p => p.Id == playerId);
        if (player == null) return (false, $"Player with id {playerId} not found.");

        if (!CanBuildExplorer(player))
        {
            _logger.LogWarning($"Player with ID {playerId} cannot afford to build an explorer.");
            return (false, $"Player with ID {playerId} cannot afford to build an explorer.");
        }

        var node = state.Board.Intersections.GetValueOrDefault(nodeId);
        if (node == null)
        {
            _logger.LogWarning($"Node with ID {nodeId} not found.");
            return (false, $"Node with ID {nodeId} not found.");
        }
        if (state.Board.IsOccupied(nodeId))
        {
            _logger.LogWarning($"Node with ID {nodeId} is already occupied.");
            return (false, $"Node with ID {nodeId} is already occupied.");
        }

        SpendResources(code, playerId, GameCosts.ExplorerCost.Keys.ToList());
        player!.ExplorerPositions.Add(nodeId);
        return (true, "Explorer built successfully.");
    }

    public (bool IsSuccess, string ErrorMessage) MoveExplorer(
        string code, Guid playerId, Guid sourcePathId, Guid targetPathId
        )
    {
        var session = _gameStore.GetSession(code);
        if (session == null) return (false, $"Session witch code {code} not found.");

        var state = session.GetGameState();
        var player = state.Players.FirstOrDefault(p => p.Id == playerId);
        if (player == null) return (false, $"Player with id {playerId} not found.");

        if (!CanMoveExplorer(player))
        {
            _logger.LogWarning($"Player with ID {playerId} cannot afford to move an explorer.");
            return (false, $"Player with ID {playerId} cannot afford to move an explorer.");
        }

        if (!player.ExplorerPositions.Contains(sourcePathId))
        {
            _logger.LogWarning($"Player with ID {playerId} has no explorer on path {sourcePathId}.");
            return (false, $"Player with ID {playerId} has no explorer on path {sourcePathId}.");
        }

        var targetPath = state.Board.Paths.GetValueOrDefault(targetPathId);
        if (targetPath == null)
        {
            _logger.LogWarning($"Path with ID {targetPathId} not found.");
            return (false, $"Path with ID {targetPathId} not found.");
        }

        if (targetPath.Requirement != null && !targetPath.Requirement.CanPass(player))
        {
            _logger.LogWarning($"Player with ID {playerId} does not meet requirements for path {targetPathId}.");
            return (false, $"Player with ID {playerId} does not meet requirements for path {targetPathId}.");
        }

        SpendResources(code, playerId, GameCosts.MoveExplorerCost.Keys.ToList());

        if (targetPath.ExplorationCounter != null)
        {
            player.ExplorationCounters.Add(targetPath.ExplorationCounter);
            targetPath.ExplorationCounter = null;
        }

        player.ExplorerPositions.Remove(sourcePathId);
        player.ExplorerPositions.Add(targetPathId);

        return (true, "Explorer moved successfully.");
    }

    public (bool IsSuccess, string ErrorMessage) AdvanceProgress(string code, Guid playerId, ProgressType progressType)
    {
        var session = _gameStore.GetSession(code);
        if (session == null) return (false, $"Session witch code {code} not found.");

        var state = session.GetGameState();
        var player = state.Players.FirstOrDefault(p => p.Id == playerId);
        if (player == null) return (false, $"Player with id {playerId} not found.");

        if (!CanAdvanceProgress(player, progressType))
        {
            _logger.LogWarning($"Player with ID {playerId} cannot afford to advance progress in {progressType}.");
            return (false, $"Player with ID {playerId} cannot afford to advance progress in {progressType}.");
        }

        int currentLevel = player.Progress.GetLevel(progressType);
        int targetLevel = currentLevel + 1;
        var cost = ProgressCosts.GetCost(progressType, targetLevel);

        SpendResources(code, playerId, cost.Keys.ToList());
        if (!player.Progress.Progress.TryAdd(progressType, targetLevel))
        {
            player.Progress.Progress[progressType] = targetLevel;
        }
        return (true, $"Progress in {progressType} advanced successfully.");
    }

    public bool CheckWinCondition(string code, Guid playerId)
    {
        var session = _gameStore.GetSession(code);
        if (session == null)
        {
            _logger.LogWarning($"Session with ID {code} not found.");
            throw new KeyNotFoundException($"Session with ID {code} not found.");
        }

        var state = session.GetGameState();
        var player = state.Players.FirstOrDefault(p => p.Id == playerId);
        if (player == null)
        {
            _logger.LogWarning($"Player with ID {playerId} not found.");
            throw new KeyNotFoundException($"Player with ID {playerId} not found.");
        }

        return state.CalculateVictoryPoints(playerId) >= GameConstants.VictoryPointsToWin;
    }

    private bool CanBuildCamp(Player player) => player.Resources.CanAfford(GameCosts.CampCost);
    private bool CanBuildExplorer(Player player) => player.Resources.CanAfford(GameCosts.ExplorerCost);
    private bool CanMoveExplorer(Player player) => player.Resources.CanAfford(GameCosts.MoveExplorerCost);
    private bool CanAdvanceProgress(Player player, ProgressType progressType)
    {
        int currentLevel = player!.Progress.GetLevel(progressType);

        if (currentLevel >= 5)
            return false;

        int targetLevel = currentLevel + 1;
        var cost = ProgressCosts.GetCost(progressType, targetLevel);

        return player!.Resources.CanAfford(cost);
    }

    private Player GetValidPlayer(GameSession? session, Guid playerId)
    {
        if (session == null)
            throw new KeyNotFoundException("Сессия не найдена.");

        var player = session.GetGameState().Players.FirstOrDefault(p => p.Id == playerId);
        if (player == null)
            throw new KeyNotFoundException($"Игрок с ID {playerId} не найден.");

        return player;
    }
}