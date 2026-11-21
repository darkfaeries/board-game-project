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

    public void SetExplorer(BoardState board, Guid playerId, AxialCoords to)
    {
        if (board.IsOccupied(to))
            throw new GameRuleException("Target node for move explorer is occupied.");

        board.ExplorerPositions.Add(to, playerId);
    }
    public ExplorationCounter? TakeExplorationCounter(GameSession session, AxialCoords from, AxialCoords to)
    {
        var board = session.GetGameState().Board;

        ExplorationCounter? expCounter = null;

        var path = new Path(from, to);
        if (board.ExplorationPaths.TryGetValue(path, out var explorationPath) &&
            explorationPath.ExplorationCounter != null)
        {
            expCounter = explorationPath.ExplorationCounter;
            explorationPath.ExplorationCounter = null;
        }


        return expCounter;
    }

    public bool TryMoveEnemy(GameSession session, EnemyType enemyType, AxialCoords? destination)
    {
        var board = session.GetGameState().Board;
        if (destination == null)
        {
            _logger.LogWarning(
                "Enemy move rejected in session {SessionCode}: {EnemyType}; destination is null.",
                session.Code, enemyType
            );
            return false;
        }

        if (!Enum.IsDefined(enemyType))
        {
            _logger.LogWarning(
                "Enemy move rejected in session {SessionCode}: {EnemyType} to ({Q}, {R}); invalid enemy type.",
                session.Code, enemyType, destination.q, destination.r
            );
            return false;
        }

        if (!board.Hexes.TryGetValue(destination, out var hex))
        {
            _logger.LogWarning(
                "Enemy move rejected in session {SessionCode}: {EnemyType} to ({Q}, {R}); hex not found.",
                session.Code, enemyType, destination.q, destination.r
            );
            return false;
        }

        if (!IsEnemyRegion(enemyType, hex.Region))
        {
            _logger.LogWarning(
                "Enemy move rejected in session {SessionCode}: {EnemyType} to ({Q}, {R}); region not allowed.",
                session.Code, enemyType, destination.q, destination.r
            );
            return false;
        }

        if (Equals(destination, GetEnemyPosition(board, enemyType)))
        {
            _logger.LogWarning(
                "Enemy move rejected in session {SessionCode}: {EnemyType} to ({Q}, {R}); enemy already at destination.",
                session.Code, enemyType, destination.q, destination.r
            );
            return false;
        }

        if (enemyType == EnemyType.Neanderthal)
            board.NeanderthalPosition = destination;
        else
            board.SabertoothTigerPosition = destination;

        _logger.LogInformation(
            "Enemy moved in session {SessionCode}: {EnemyType} to ({Q}, {R}).",
            session.Code, enemyType, destination.q, destination.r
        );
        return true;
    }

    public bool IsEnemyAt(GameSession session, EnemyType enemyType, AxialCoords? destination)
    {
        var board = session.GetGameState().Board;
        return destination != null && Enum.IsDefined(enemyType) &&
            board.Hexes.TryGetValue(destination, out var hex) &&
            IsEnemyRegion(enemyType, hex.Region) &&
            Equals(destination, GetEnemyPosition(board, enemyType));
    }

    public IReadOnlyCollection<Guid> GetAdjacentCampOwnerIds(GameSession session, AxialCoords destination)
    {
        var board = session.GetGameState().Board;
        var neighbors = destination.HexGetNeighbors();
        return board.CampPositions
            .Where(camp => neighbors.Contains(camp.Key))
            .Select(camp => camp.Value)
            .Distinct()
            .ToArray();
    }

    public bool HasDesertificationTarget(GameSession session, TerrainType terrain)
    {
        var board = session.GetGameState().Board;
        return Enum.IsDefined(terrain) && board.Hexes.Values.Any(hex =>
            hex.Region == Region.Africa && hex.Terrain == terrain && !hex.IsDesertified);
    }

    public bool TryDesertifyHex(GameSession session, TerrainType terrain, AxialCoords? hexCoords)
    {
        var board = session.GetGameState().Board;
        if (hexCoords == null)
        {
            _logger.LogWarning(
                "Desertification rejected in session {SessionCode}: {Terrain} at ({Q}, {R}); coordinates are null.",
                session.Code, terrain, hexCoords?.q, hexCoords?.r
            );
            return false;
        }

        if (!Enum.IsDefined(terrain))
        {
            _logger.LogWarning(
                "Desertification rejected in session {SessionCode}: {Terrain} at ({Q}, {R}); invalid terrain type.",
                session.Code, terrain, hexCoords.q, hexCoords.r
            );
            return false;
        }

        if (!board.Hexes.TryGetValue(hexCoords, out var hex))
        {
            _logger.LogWarning("Desertification rejected in session {SessionCode}: {Terrain} at ({Q}, {R}); hex not found.",
                session.Code, terrain, hexCoords.q, hexCoords.r
            );
            return false;
        }

        if (hex.Region != Region.Africa)
        {
            _logger.LogWarning(
                "Desertification rejected in session {SessionCode}: {Terrain} at ({Q}, {R}); hex is not in Africa.",
                session.Code, terrain, hexCoords.q, hexCoords.r
            );
            return false;
        }

        if (hex.Terrain != terrain)
        {
            _logger.LogWarning(
                "Desertification rejected in session {SessionCode}: {Terrain} at ({Q}, {R}); terrain does not match.",
                session.Code, terrain, hexCoords.q, hexCoords.r
            );
            return false;
        }

        if (hex.IsDesertified)
        {
            _logger.LogWarning(
                "Desertification rejected in session {SessionCode}: {Terrain} at ({Q}, {R}); hex is already desertified.",
                session.Code, terrain, hexCoords.q, hexCoords.r
            );
            return false;
        }

        hex.IsDesertified = true;
        _logger.LogInformation(
            "Hex desertified in session {SessionCode}: {Terrain} at ({Q}, {R}).",
            session.Code, terrain, hexCoords.q, hexCoords.r
        );
        return true;
    }

    private static bool IsEnemyRegion(EnemyType enemyType, Region region) => enemyType switch
    {
        EnemyType.Neanderthal => region is Region.Africa or Region.Eurasia,
        EnemyType.SabertoothTiger => region is Region.America or Region.Australia,
        _ => false
    };

    private static AxialCoords? GetEnemyPosition(BoardState board, EnemyType enemyType) => enemyType switch
    {
        EnemyType.Neanderthal => board.NeanderthalPosition,
        EnemyType.SabertoothTiger => board.SabertoothTigerPosition,
        _ => null
    };
}
