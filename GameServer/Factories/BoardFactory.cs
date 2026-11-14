namespace GameServer;

public class BoardFactory : IBoardFactory
{
    private readonly ILogger<BoardFactory> _logger;
    private readonly CsvBoardGenerator _csvBoardGenerator;

    public BoardFactory(ILogger<BoardFactory> logger, CsvBoardGenerator csvBoardGenerator)
    {
        _logger = logger;
        _csvBoardGenerator = csvBoardGenerator;
    }

    public BoardState GetBoardState(MapType mapType)
    {
        switch (mapType)
        {
            case MapType.Classic:
                return GetClassicBoard();
            case MapType.Earth:
                return GetEarthBoard();
            case MapType.BySeed:
                return GetBySeed();
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
        // TODO
        throw new NotImplementedException();
    }

    public BoardState GetBySeed()
    {
        //TODO
        throw new NotImplementedException();
    }
}
