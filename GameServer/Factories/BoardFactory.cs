namespace GameServer;

public class BoardFactory : IBoardFactory
{
    private readonly string _mapsPath;
    private readonly ILogger<BoardFactory> _logger;

    public BoardFactory(string mapsPath, ILogger<BoardFactory> logger)
    {
        _mapsPath = mapsPath;
        _logger = logger;
    }

    public BoardState GetClassicBoard()
    {
        _logger.LogInformation("Loading classic board from {MapsPath}.", _mapsPath);

        var mapPath = System.IO.Path.Combine(_mapsPath, "Default_Map");
        var hexesPath = System.IO.Path.Combine(mapPath, "hexes.csv");
        var nodesPath = System.IO.Path.Combine(mapPath, "nodes.csv");
        var pathsPath = System.IO.Path.Combine(mapPath, "paths.csv");

        using var hexesReader = new StreamReader(hexesPath);
        using var nodesReader = new StreamReader(nodesPath);
        using var pathsReader = new StreamReader(pathsPath);

        var board = new BoardState(hexesReader, nodesReader, pathsReader);
        _logger.LogInformation(
            "Classic board loaded: {HexCount} hexes, {NodeCount} nodes, {PathCount} paths.",
            board.Hexes.Count,
            board.Nodes.Count,
            board.ExplorationPaths.Count);

        return board;
    }
}
