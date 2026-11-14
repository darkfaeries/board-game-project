namespace GameServer;

public class BoardService : IBoardService
{
    private readonly string _mapsPath;
    private readonly ILogger<BoardService> _logger;

    public BoardService(string mapsPath, ILogger<BoardService> logger)
    {
        _mapsPath = mapsPath;
        _logger = logger;
    }

    public void SetupBoard(GameSession session, int playersCount)
    {
            var board = session.GetGameState().Board;

            var eurasiaHexes = board.Hexes.Values
                .Where(hex => hex.Region == Region.Eurasia)
                .ToArray();

            var americaHexes = board.Hexes.Values
                .Where(hex => hex.Region == Region.America)
                .ToArray();

            if (eurasiaHexes.Length == 0 || americaHexes.Length == 0)
            {
                _logger.LogWarning("Invalid Board State: to place enemies, needed hexes from Eurasia and America.");
                throw new InvalidOperationException(
                    "Invalid Board State: to place enemies, needed hexes from Eurasia and America.");
            }

            board.NeanderthalPosition =
                eurasiaHexes[Random.Shared.Next(eurasiaHexes.Length)].Coords;

            _logger.LogInformation($"Neanderthal Position have been set: {board.NeanderthalPosition}");

            board.SabertoothTigerPosition =
                americaHexes[Random.Shared.Next(americaHexes.Length)].Coords;

            _logger.LogInformation($"Sabertooth Tiger Position have been set: {board.SabertoothTigerPosition}");

            foreach (var node in board.Nodes.Values)
            {
                node.HasTribeCounter = playersCount == 3 ? node.CanSettle3p : node.CanSettle;
            }
    }

    public bool PlaceInitialCamp(GameSession session, Guid playerId, AxialCoords nodeCoords)
    {
        var board = session.GetGameState().Board;
        if (nodeCoords == null || !board.Nodes.TryGetValue(nodeCoords, out var node) ||
            !node.IsStartSpace || board.IsOccupied(nodeCoords) ||
            board.CampPositions.Values.Count(id => id == playerId) >= GameConstants.InitialCampsCount)
            return false;

        board.CampPositions.Add(nodeCoords, playerId);

        _logger.LogInformation($"Initial camp for player {playerId} placed on {nodeCoords}");

        return true;
    }

    public Tribe PlaceCamp(GameSession session, Guid  playerId, AxialCoords nodeCoords)
    {
        var board = session.GetGameState().Board;
        var explorerPositions = board.ExplorerPositions;

        if (nodeCoords == null)
        {
            _logger.LogWarning("Null coordinates for Camp.");
            throw new GameRuleException("Null coordinates for Camp.");
        }

        if (!explorerPositions.TryGetValue(nodeCoords, out var ownerId) ||
            !playerId.Equals(ownerId))
        {
            _logger.LogWarning($"Player with ID {playerId} has no explorer at the selected node.");
            throw new GameRuleException($"No explorer at the selected node.");
        }

        if (!board.Nodes.TryGetValue(nodeCoords, out var node) || !node.HasTribeCounter || node.Tribe == null)
        {
            _logger.LogWarning($"Node with coordinates ({nodeCoords.q}, {nodeCoords.r}) has no tribe counter.");
            throw new GameRuleException("Node for camp has no tribe counter.");
        }

        board.ExplorerPositions.Remove(nodeCoords);
        board.CampPositions.Add(nodeCoords, playerId);

        var tribe = node.Tribe.Value;
        node.HasTribeCounter = false;

        _logger.LogInformation(
            $"Converted explorer to camp for player {playerId} at ({nodeCoords.q}, {nodeCoords.r}).");

        return tribe;
    }

    public void PlaceExplorer(GameSession session, Guid  playerId, AxialCoords nodeCoords)
    {
        var board = session.GetGameState().Board;
        if (board.ExplorerPositions.Values.Count(ownerId => ownerId == playerId) >= 2)
        {
            _logger.LogWarning($"Player with ID {playerId} already has two explorers.");
            throw new GameRuleException("A player cannot have more than two explorers.");
        }

        if (nodeCoords == null)
        {
            _logger.LogWarning("Invalid coordinates for Explorer.");
            throw new GameRuleException("Invalid coordinates for Explorer");
        }

        if (!board.Nodes.ContainsKey(nodeCoords))
        {
            _logger.LogWarning($"Node with coordinates ({nodeCoords.q}, {nodeCoords.r}) not found.");
            throw new GameRuleException($"Node for place explorer not found.");
        }

        if (board.IsOccupied(nodeCoords))
        {
            _logger.LogWarning($"Node with coordinates ({nodeCoords.q}, {nodeCoords.r}) is already occupied.");
            throw new GameRuleException("Node is already occupied.");
        }

        if (!(nodeCoords.NodeGetNodeNeighbors().Any(coord =>
            board.CampPositions.TryGetValue(coord, out var owner)
            && owner.Equals(playerId))))
        {
            _logger.LogWarning(
                $"Node with coordinates ({nodeCoords.q}, {nodeCoords.r}) is not adjacent to player {playerId} camp.");
            throw new GameRuleException("An explorer must be placed adjacent to one of your camps.");
        }

        board.ExplorerPositions.Add(nodeCoords, playerId);
    }

    public (int ClothingReq, int ShelterReq) CheckRoute(
        GameSession session, Guid playerId, List<AxialCoords> route)
    {
        var board = session.GetGameState().Board;

        var sourceCoords = route[0];
        var targetCoords = route[^1];

        if (!board.ExplorerPositions.TryGetValue(sourceCoords, out var ownerId)
            || !ownerId.Equals(playerId))
        {
            _logger.LogWarning($"Player with ID {playerId} has no explorer at the source node.");
            throw new GameRuleException("No explorer at the source node.");
        }

        if (board.IsOccupied(targetCoords))
        {
            _logger.LogWarning($"Target node ({targetCoords.q}, {targetCoords.r}) is occupied.");
             throw new GameRuleException("Target node fot move explorer is occupied.");
        }

        var clothReq = 0;
        var shelterReq = 0;

        for (int i = 1; i < route.Count; i++)
        {
            var from = route[i - 1];
            var to = route[i];

            if (!board.Nodes.ContainsKey(from) ||
                !board.Nodes.ContainsKey(to) ||
                !from.NodeGetNodeNeighbors().Contains(to))
            {
                _logger.LogWarning($"No adjacent nodes exist between ({from.q}, {from.r}) and ({to.q}, {to.r}).");
                throw new GameRuleException("Invalid route for move explorer.");
            }

            var path = new Path(from, to);

            if (board.BlockedPaths.Contains(path))
            {
                _logger.LogWarning($"Path between ({from.q}, {from.r}) and ({to.q}, {to.r}) is blocked.");
                throw new GameRuleException("Invalid route for move explorer.");
            }

            if (board.ExplorationPaths.TryGetValue(path, out var explorationPath))
            {
                clothReq = Math.Max(clothReq, explorationPath.ClothingRequirement);
                shelterReq = Math.Max(shelterReq, explorationPath.ShelterRequirement);
            }
        }

        return (clothReq, shelterReq);
    }

    public List<ExplorationCounter> MoveExplorer(GameSession session, Guid playerId, List<AxialCoords> route)
    {
        var board = session.GetGameState().Board;

        var expCounters = new List<ExplorationCounter>{};

        for (int i = 1; i < route.Count; i++)
        {
            var from = route[i - 1];
            var to = route[i];

            var path = new Path(from, to);
            if (board.ExplorationPaths.TryGetValue(path, out var explorationPath) &&
                explorationPath.ExplorationCounter != null)
            {
                expCounters.Add(explorationPath.ExplorationCounter);
                explorationPath.ExplorationCounter = null;
            }
        }

        board.ExplorerPositions.Remove(route[0]);
        board.ExplorerPositions.Add(route[^1], playerId);

        return expCounters;
    }
}
