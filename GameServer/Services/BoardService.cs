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
        lock (session.Gate)
        {
            var board = session.GetGameState().Board;

            foreach (var node in board.Nodes.Values)
            {
                node.HasTribeCounter = playersCount == 3 ? node.CanSettle3p : node.CanSettle;
            }

            var eurasiaHexes = board.Hexes.Values
                .Where(hex => hex.Region == Region.Eurasia)
                .ToArray();

            var americaHexes = board.Hexes.Values
                .Where(hex => hex.Region == Region.America)
                .ToArray();

            if (eurasiaHexes.Length == 0 || americaHexes.Length == 0)
            {
                throw new InvalidOperationException(
                    "Invalid Board State: to place enemies, needed hexes from Eurasia and America.");
            }

            board.NeanderthalPosition =
                eurasiaHexes[Random.Shared.Next(eurasiaHexes.Length)].Coords;

            board.SabertoothTigerPosition =
                americaHexes[Random.Shared.Next(americaHexes.Length)].Coords;
        }
    }

    public bool PlaceInitialCamp(GameSession session, Guid playerId, AxialCoords nodeCoords)
    {
        lock (session.Gate)
        {
            var state = session.GetGameState();
            if (state.Status != GameStatus.Setup || state.CurrentPlayerId != playerId || state.GetPlayer(playerId) == null ||
                nodeCoords == null || !state.Board.Nodes.TryGetValue(nodeCoords, out var node) ||
                !node.IsStartSpace || state.Board.IsOccupied(nodeCoords) ||
                state.Board.CampPositions.Values.Count(id => id == playerId) >= GameConstants.InitialCampsCount)
                return false;

            state.Board.CampPositions.Add(nodeCoords, playerId);
            return true;
        }
    }
}
