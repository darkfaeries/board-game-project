namespace GameServer;

public interface IBoardService
{
    void SetupBoard(GameSession session, int playersCount);
    bool PlaceInitialCamp(GameSession session, Guid playerId, AxialCoords nodeCoords);
    Tribe PlaceCamp(GameSession session, Guid playerId, AxialCoords nodeCoords);
    void PlaceExplorer(GameSession session, Guid playerId, AxialCoords nodeCoords);
    (int ClothingReq, int ShelterReq) CheckRoute(
            GameSession session, Guid playerId, List<AxialCoords> route);
    void SetExplorer(BoardState board, Guid playerId, AxialCoords to);
    ExplorationCounter? TakeExplorationCounter(GameSession session, AxialCoords from, AxialCoords to);
    bool TryMoveEnemy(GameSession session, EnemyType enemyType, AxialCoords? destination);
    bool IsEnemyAt(GameSession session, EnemyType enemyType, AxialCoords? destination);
    IReadOnlyCollection<Guid> GetAdjacentCampOwnerIds(GameSession session, AxialCoords destination);
    bool HasDesertificationTarget(GameSession session, TerrainType terrain);
    bool TryDesertifyHex(GameSession session, TerrainType terrain, AxialCoords? hexCoords);
}
