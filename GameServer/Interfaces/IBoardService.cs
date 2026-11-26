namespace GameServer;

public interface IBoardService
{
    void SetupBoard(GameSession session, int playersCount);
    bool PlaceInitialCamp(GameSession session, Guid playerId, AxialCoords nodeCoords);
    Tribe PlaceCamp(GameSession session, Guid playerId, AxialCoords nodeCoords);
    void PlaceExplorer(GameSession session, Guid playerId, AxialCoords nodeCoords);
    (int ClothingReq, int ShelterReq) CheckRoute(
            GameSession session, Guid playerId, List<AxialCoords> route);
    List<ExplorationCounter> MoveExplorer(GameSession session, Guid playerId, List<AxialCoords> route);
}
