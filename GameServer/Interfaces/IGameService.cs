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
    MoveResult MoveExplorer(GameSession session, Guid playerId, List<AxialCoords> route);
    bool SubmitChooseEnemy(GameSession session, Guid playerId, Guid decisionId, EnemyType enemyType);
    (bool isSuccess, MoveResult? moveResult) SubmitChooseEnemyDestination(
        GameSession session,
        Guid playerId,
        Guid decisionId,
        AxialCoords? destination
    );
    (bool isSuccess, MoveResult? moveResult) SubmitChooseStealTarget(
        GameSession session,
        Guid playerId,
        Guid decisionId,
        Guid? targetPlayerId
    );
    (bool isSuccess, MoveResult? moveResult) SubmitChooseDesertificationHex(
        GameSession session,
        Guid playerId,
        Guid decisionId,
        AxialCoords? hexCoords
    );
}
