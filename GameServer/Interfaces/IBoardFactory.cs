namespace GameServer;

public interface IBoardFactory
{
    BoardState GetBoardState(MapType mapType, int? seed = null);
}
