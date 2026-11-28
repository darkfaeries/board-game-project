namespace GameServer;

public interface IGameService
{
    bool StartGame(GameSession session);
    bool SubmitInitialPlacement(GameSession session, Guid playerId, AxialCoords nodeCoords);
    void OnSevenRolled(GameSession session);
    public bool SubmitDiscardResources(
        GameSession session,
        Guid playerId,
        Dictionary<ResourceType, int> selectedResources);
    void ConvertExplorerToCamp(GameSession session, Guid playerId, AxialCoords nodeCoords);
    void CreateExplorer(GameSession session, Guid playerId, AxialCoords nodeCoords);
    List<AxialCoords> MoveExplorer(GameSession session, Guid playerId, List<AxialCoords> route);
    bool SubmitChooseEnemy(GameSession session, Guid playerId, Guid decisionId, EnemyType enemyType);
    (bool isSuccess, List<AxialCoords>? moveResult) SubmitChooseEnemyDestination(
        GameSession session,
        Guid playerId,
        Guid decisionId,
        AxialCoords? destination
    );
    (bool isSuccess, List<AxialCoords>? moveResult) SubmitChooseStealTarget(
        GameSession session,
        Guid playerId,
        Guid decisionId,
        Guid? targetPlayerId
    );
    (bool isSuccess, List<AxialCoords>? moveResult) SubmitChooseDesertificationHex(
        GameSession session,
        Guid playerId,
        Guid decisionId,
        AxialCoords? hexCoords
    );
}
