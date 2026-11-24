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

    public BoardState GetClassicBoard()
    {
        _logger.LogInformation("Generating classic board.");
        return _csvBoardGenerator.GenerateBoard("Default_Map");
    }
}
