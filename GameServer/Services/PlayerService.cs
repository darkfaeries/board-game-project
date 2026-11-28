namespace GameServer;

public class PlayerService : IPlayerService
{
    private ILogger<PlayerService> _logger;

    public PlayerService(ILogger<PlayerService> logger)
    {
        _logger = logger;
    }

    public void DistributeHexResources(GameSession session, int diceValue)
    {
        if (diceValue == 7)
        {
            _logger.LogInformation("Skipping resource distribution for dice value 7.");
            return;
        }

        var state = session.GetGameState();

        var activeHexes = state.Board.Hexes.Values
        .Where(h => h.NumberToken == diceValue && !h.IsDesertified)
        .ToList();

        foreach (var hex in activeHexes)
        {
            var resourceType = hex.GetProducedResource();
            var adjacentNodes = hex.Coords.HexGetNeighbors();

            var campPositions = state.Board.CampPositions;

            foreach (var nodeCoords in adjacentNodes)
            {
                if (campPositions.TryGetValue(nodeCoords, out var playerId))
                {
                    AddResources(session, playerId, new Dictionary<ResourceType, int>
                    {
                        [resourceType] = 1
                    });
                }
            }
        }
    }

    public void AddResources(
        GameSession session, Guid playerId, Dictionary<ResourceType, int> resources)
    {
        ValidateResourceAmounts(resources);
        var player = session.GetGameState().GetPlayer(playerId);
        if (player == null)
        {   
            _logger.LogWarning($"Player with ID {playerId} not found.");
            throw new KeyNotFoundException($"Player with ID {playerId} not found.");
        }

        foreach (var (resource, amount) in resources)
        {
            player.Resources.Add(resource, amount);
        }

        _logger.LogInformation($"Added resources to player {playerId}.");
    }

    public void SpendResources(
        GameSession session, Guid playerId, Dictionary<ResourceType, int> resources)
    {   
        ValidateResourceAmounts(resources);
        var player = session.GetGameState().GetPlayer(playerId);
        if (player == null)
        {   
            _logger.LogWarning($"Player with ID {playerId} not found.");
            throw new KeyNotFoundException($"Player with ID {playerId} not found.");
        }

        if (!player.Resources.CanAfford(resources))
        {
            _logger.LogWarning($"Player with ID {playerId} cannot afford requested resources.");
            throw new InvalidOperationException($"Player with ID {playerId} cannot afford requested resources.");
        }

        foreach (var (resource, amount) in resources)
        {
            player.Resources.Spend(resource, amount);
        }

        _logger.LogInformation($"Spent resources from player {playerId}.");
    }

    public void TradeWithBank(
        GameSession session,
        Guid playerId,
        Dictionary<ResourceType, int> resourcesToGive,
        ResourceType resourceToGet)
    {
        if (resourcesToGive.Keys.Count != 3)
        {
            _logger.LogWarning($"Player {playerId} must give exactly 3 resources to trade with the bank.");
            throw new ArgumentException("Must give exactly 3 resources to trade with the bank.");
        }

        SpendResources(session, playerId, resourcesToGive);
        AddResources(
            session,
            playerId,
            new Dictionary<ResourceType, int> { { resourceToGet, 1 } });

        _logger.LogInformation($"Player {playerId} completed a bank trade.");
    }

    public (bool IsSuccess, string ErrorMessage) AdvanceProgress(
        GameSession session, Guid playerId, ProgressType progressType)
    {
        var state = session.GetGameState();
        var player = state.GetPlayer(playerId);
        if (player == null)
        {
            _logger.LogWarning($"Player with ID {playerId} not found.");
            return (false, $"Player with id {playerId} not found.");
        }

        if (!CanAdvanceProgress(player, progressType))
        {
            _logger.LogWarning($"Player with ID {playerId} cannot afford to advance progress in {progressType}.");
            return (false, $"Player with ID {playerId} cannot afford to advance progress in {progressType}.");
        }

        int currentLevel = player.Progress.GetLevel(progressType);
        int targetLevel = currentLevel + 1;
        var cost = GameCosts.AdvanceLevelCost(targetLevel);

        SpendResources(session, playerId, cost);
        if (!player.Progress.Progress.TryAdd(progressType, targetLevel))
        {
            player.Progress.Progress[progressType] = targetLevel;
        }

        _logger.LogInformation(
            $"Player {playerId} advanced {progressType} progress to level {targetLevel}.");
        return (true, $"Progress in {progressType} advanced successfully.");
    }

    public bool CheckWinCondition(GameSession session, Guid playerId)
    {
        var state = session.GetGameState();
        var player = state.Players.FirstOrDefault(p => p.Id == playerId);
        if (player == null)
        {
            _logger.LogWarning($"Player with ID {playerId} not found.");
            throw new KeyNotFoundException($"Player with ID {playerId} not found.");
        }

        var isWinner = state.CalculateVictoryPoints(playerId) >= GameConstants.VictoryPointsToWin;
        _logger.LogInformation($"Checked win condition for player {playerId}: {isWinner}.");
        return isWinner;
    }

    private void ValidateResourceAmounts(Dictionary<ResourceType, int> resources)
    {
        if (resources == null || resources.Count == 0 ||
            resources.Any(resource => resource.Value <= 0))
        {
            _logger.LogWarning("Invalid resource amounts supplied.");
            throw new ArgumentException("Resource amounts must be greater than zero.", nameof(resources));
        }
    }

    public bool CanBuildCamp(Player player) => player.Resources.CanAfford(GameCosts.CampCost);
    public bool CanCreateExplorer(Player player) => player.Resources.CanAfford(GameCosts.ExplorerCost);
    public bool CanAfford(Player player, Dictionary<ResourceType, int>  cost)
    {
        return player.Resources.CanAfford(cost);
    }

    public Dictionary<ResourceType, int> CalculateMoveCost(Player player, int steps)
    {
        var movementRange = GameCosts.ExplorerBaseMovementRange + player.Progress.GetLevel(ProgressType.Food);
        var payments = steps / movementRange + (steps % movementRange == 0 ? 0 : 1);

        return GameCosts.MoveExplorerCost.ToDictionary(
            entry => entry.Key,
            entry => entry.Value * payments
        );

    }

    public bool MeetsTravelRequirements(
        Player player,
        int clothingRequired,
        int shelterRequired)
    {
        return player.Progress.GetLevel(ProgressType.Clothing) >= clothingRequired
            && player.Progress.GetLevel(ProgressType.Shelter) >= shelterRequired;
    }

    private bool CanAdvanceProgress(Player player, ProgressType progressType)
    {
        int currentLevel = player!.Progress.GetLevel(progressType);

        if (currentLevel >= 5)
            return false;

        int targetLevel = currentLevel + 1;
        var cost = GameCosts.AdvanceLevelCost(targetLevel);

        return player!.Resources.CanAfford(cost);
    }
}