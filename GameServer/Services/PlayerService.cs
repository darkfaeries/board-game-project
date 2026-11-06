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

            foreach (var nodeCoords in adjacentNodes)
            {
                foreach (var owner in state.Players)
                {
                    var campsAtNode = owner.CampPositions.Count(camp => camp.Equals(nodeCoords));
                    if (campsAtNode == 0) continue;

                    owner.Resources.Add(resourceType, campsAtNode);
                    _logger.LogInformation(
                        $"Added {campsAtNode} {resourceType} to player {owner.Id} from hex ({hex.Coords.q}, {hex.Coords.r}).");
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

    public (bool IsSuccess, string ErrorMessage) ConvertExplorerToCamp(
        GameSession session, Guid playerId, AxialCoords nodeCoords)
    {
        var state = session.GetGameState();
        var player = state.GetPlayer(playerId);
        if (player == null)
        {
            _logger.LogWarning($"Player with ID {playerId} not found.");
            return (false, $"Player with id {playerId} not found.");
        }

        if (!player.ExplorerPositions.Contains(nodeCoords))
        {
            _logger.LogWarning($"Player with ID {playerId} has no explorer at the selected node.");
            return (false, "The player has no explorer at the selected node.");
        }

        if (!CanBuildCamp(player))
        {
            _logger.LogWarning($"Player with ID {playerId} cannot afford to build a camp.");
            return (false, "The player cannot afford to build a camp.");
        }

        if (!state.Board.Nodes.TryGetValue(nodeCoords, out var node) || !node.HasTribeCounter)
        {
            _logger.LogWarning($"Node with coordinates ({nodeCoords.q}, {nodeCoords.r}) has no tribe counter.");
            return (false, "An explorer can be converted only on a node with a tribe counter.");
        }

        if (node.Tribe == null)
        {
            _logger.LogWarning($"Node with coordinates ({nodeCoords.q}, {nodeCoords.r}) has no tribe.");
            return (false, "The selected node has no tribe.");
        }

        player.ExplorerPositions.Remove(nodeCoords);
        player.CampPositions.Add(nodeCoords);
        player.TribesCollected.Add(node.Tribe.Value);
        node.HasTribeCounter = false;
        _logger.LogInformation(
            $"Converted explorer to camp for player {playerId} at ({nodeCoords.q}, {nodeCoords.r}).");
        return (true, "Explorer converted to camp successfully.");
    }

    public (bool IsSuccess, string ErrorMessage) CreateExplorer(
        GameSession session, Guid playerId, AxialCoords nodeCoords)
    {
        var state = session.GetGameState();
        var player = state.GetPlayer(playerId);
        if (player == null)
        {
            _logger.LogWarning($"Player with ID {playerId} not found.");
            return (false, $"Player with id {playerId} not found.");
        }

        if (!CanCreateExplorer(player))
        {
            _logger.LogWarning($"Player with ID {playerId} cannot afford to build an explorer.");
            return (false, $"Player with ID {playerId} cannot afford to build an explorer.");
        }

        if (player.ExplorerPositions.Count >= GameConstants.MaxExplorers)
        {
            _logger.LogWarning($"Player with ID {playerId} already has two explorers.");
            return (false, "A player cannot have more than two explorers.");
        }

        if (!state.Board.Nodes.ContainsKey(nodeCoords))
        {
            _logger.LogWarning($"Node with coordinates ({nodeCoords.q}, {nodeCoords.r}) not found.");
            return (false, $"Node with coordinates ({nodeCoords.q}, {nodeCoords.r}) not found.");
        }

        if (state.Board.IsOccupied(nodeCoords))
        {
            _logger.LogWarning($"Node with coordinates ({nodeCoords.q}, {nodeCoords.r}) is already occupied.");
            return (false, "Node is already occupied.");
        }

        if (!player.CampPositions.Any(camp =>
                camp.NodeGetNodeNeighbors().Contains(nodeCoords)))
        {
            _logger.LogWarning(
                $"Node with coordinates ({nodeCoords.q}, {nodeCoords.r}) is not adjacent to player {playerId} camp.");
            return (false, "An explorer must be placed adjacent to one of your camps.");
        }

        SpendResources(session, playerId, GameCosts.ExplorerCost);
        player.ExplorerPositions.Add(nodeCoords);
        _logger.LogInformation(
            $"Created explorer for player {playerId} at ({nodeCoords.q}, {nodeCoords.r}).");
        return (true, "Explorer created successfully.");
    }

    public (bool IsSuccess, string ErrorMessage) MoveExplorer(
        GameSession session, Guid playerId, List<AxialCoords> route
        )
    {
        var state = session.GetGameState();
        var player = state.GetPlayer(playerId);
        if (player == null)
        {
            _logger.LogWarning($"Player with ID {playerId} not found.");
            return (false, $"Player with id {playerId} not found.");
        }

        if (!CanMoveExplorer(player))
        {
            _logger.LogWarning($"Player with ID {playerId} cannot afford to move an explorer.");
            return (false, $"Player with ID {playerId} cannot afford to move an explorer.");
        }

        if (route.Count != 2)
        {
            _logger.LogWarning($"Player with ID {playerId} provided an invalid explorer route.");
            return (false, "The route must contain exactly one edge.");
        }

        var sourceCoords = route[0];
        var targetCoords = route[^1];
        if (!player.ExplorerPositions.Contains(sourceCoords))
        {
            _logger.LogWarning($"Player with ID {playerId} has no explorer at the source node.");
            return (false, "The player has no explorer at the source node.");
        }

        if (route.Count - 1 > 2 + player.Progress.GetLevel(ProgressType.Food))
        {
            _logger.LogWarning($"Player with ID {playerId} provided a route that is too long.");
            return (false, "The explorer cannot move that far with the current food progress.");
        }

        if (state.Board.IsOccupied(targetCoords))
        {
            _logger.LogWarning($"Target node ({targetCoords.q}, {targetCoords.r}) is occupied.");
            return (false, "An explorer must finish movement on an empty node.");
        }

        if (!state.Board.Nodes.ContainsKey(sourceCoords) ||
            !state.Board.Nodes.ContainsKey(targetCoords) ||
            !sourceCoords.NodeGetNodeNeighbors().Contains(targetCoords))
        {
            _logger.LogWarning($"No adjacent nodes exist between ({sourceCoords.q}, {sourceCoords.r}) and ({targetCoords.q}, {targetCoords.r}).");
            return (false, "The explorer can move only between adjacent nodes.");
        }

        var path = new Path(sourceCoords, targetCoords);
        if (state.Board.BlockedPaths.Contains(path))
        {
            _logger.LogWarning($"Path between ({sourceCoords.q}, {sourceCoords.r}) and ({targetCoords.q}, {targetCoords.r}) is blocked.");
            return (false, "There is no passable path along the selected route.");
        }

        if (state.Board.ExplorationPaths.TryGetValue(path, out var explorationPath))
        {
            if (player.Progress.GetLevel(ProgressType.Clothing) < explorationPath.ClothingRequirement ||
                player.Progress.GetLevel(ProgressType.Shelter) < explorationPath.ShelterRequirement)
            {
                _logger.LogWarning($"Player with ID {playerId} does not meet the route requirements.");
                return (false, "The player does not meet the path requirements.");
            }
        }

        SpendResources(session, playerId, GameCosts.MoveExplorerCost);

        if (state.Board.ExplorationPaths.TryGetValue(path, out explorationPath) &&
            explorationPath.ExplorationCounter != null)
        {
            player.ExplorationCounters.Add(explorationPath.ExplorationCounter);
            explorationPath.ExplorationCounter = null;
        }

        player.ExplorerPositions.Remove(sourceCoords);
        player.ExplorerPositions.Add(targetCoords);

        _logger.LogInformation(
            $"Moved explorer for player {playerId} from ({sourceCoords.q}, {sourceCoords.r}) to ({targetCoords.q}, {targetCoords.r}).");
        return (true, "Explorer moved successfully.");
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

        var isWinner = state.CalculateVictoryPoints(playerId);
        _logger.LogInformation($"Checked win condition for player {playerId}: {isWinner}.");
        return isWinner;
    }

    private void ValidateResourceAmounts(Dictionary<ResourceType, int> resources)
    {
        if (resources.Count == 0 ||
            resources.Any(resource => resource.Value <= 0))
        {
            _logger.LogWarning("Invalid resource amounts supplied.");
            throw new ArgumentException("Resource amounts must be greater than zero.", nameof(resources));
        }
    }

    private bool CanBuildCamp(Player player) => player.Resources.CanAfford(GameCosts.CampCost);
    private bool CanCreateExplorer(Player player) => player.Resources.CanAfford(GameCosts.ExplorerCost);
    private bool CanMoveExplorer(Player player) => player.Resources.CanAfford(GameCosts.MoveExplorerCost);
    private bool CanAdvanceProgress(Player player, ProgressType progressType)
    {
        int currentLevel = player!.Progress.GetLevel(progressType);

        if (currentLevel >= GameConstants.MaxProgressLevel)
            return false;

        int targetLevel = currentLevel + 1;
        var cost = GameCosts.AdvanceLevelCost(targetLevel);

        return player!.Resources.CanAfford(cost);
    }
}