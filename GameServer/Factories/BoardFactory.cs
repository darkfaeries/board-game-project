namespace GameServer;

public class BoardFactory : IBoardFactory
{
    private readonly ILogger<BoardFactory> _logger;
    private readonly CsvBoardGenerator _csvBoardGenerator;
    private readonly IHeightSource _earthHeights;
    private readonly ILoggerFactory _loggerFactory;

    public BoardFactory(ILogger<BoardFactory> logger, CsvBoardGenerator csvBoardGenerator,
        IHeightSource earthHeights, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _csvBoardGenerator = csvBoardGenerator;
        _earthHeights = earthHeights;
        _loggerFactory = loggerFactory;
    }

    public BoardState GetBoardState(MapType mapType, int? seed = null)
    {
        switch (mapType)
        {
            case MapType.Classic:
                return GetClassicBoard();
            case MapType.Earth:
                return GetEarthBoard();
            case MapType.BySeed:
                return GetBySeed(seed);
            default:
                _logger.LogError($"Invalid map type {mapType}");
                throw new ArgumentOutOfRangeException(nameof(mapType), mapType, null);
        }
    }

    public BoardState GetClassicBoard()
    {
        _logger.LogInformation("Generating classic board.");
        return _csvBoardGenerator.GenerateBoard("Default_Map");
    }

    public BoardState GetEarthBoard()
    {
        _logger.LogInformation("Generating Earth board.");
        var heights = new CroppedHeightSource(
            _loggerFactory.CreateLogger<CroppedHeightSource>(),
            _earthHeights, (0f, 0f), (1f, 5f / 6f));
        var map = new MapGenerator().Generate(
            new MapGenerationOptions(56, 20, 1f / 255f), heights,
            _loggerFactory.CreateLogger<MapGenerator>());
        var board = new BoardBuilder().Build(map, loggerFactory: _loggerFactory);
        _logger.LogInformation("Generated Earth board with {LandCount} land hexes.", board.Hexes.Count);
        return board;
    }

    public BoardState GetBySeed(int? seed = null)
    {
        int mapSeed = seed ?? Random.Shared.Next();
        _logger.LogInformation("Generating board with seed {Seed}.", mapSeed);
        var map = new SeedMapGenerator().Generate(
            new MapGenerationOptions(40, 24, 0.5f), mapSeed,
            _loggerFactory.CreateLogger<SeedMapGenerator>());
        var board = new BoardBuilder().Build(map, mapSeed, _loggerFactory);
        _logger.LogInformation("Generated board with seed {Seed} and {LandCount} land hexes.",
            mapSeed, board.Hexes.Count);
        return board;
    }
}
