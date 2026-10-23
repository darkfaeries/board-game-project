namespace GameServer;

public interface IBoardService
{
    void SetupBoard(GameSession session, int playersCount);
    bool PlaceInitialCamp(GameSession session, Guid playerId, AxialCoords nodeCoords);
}
