namespace GameServer;

public interface IPlayerService
{
    void DistributeHexResources(GameSession session, int diceValue);

    void AddResources(GameSession session, Guid playerId, List<ResourceType> resources);
    void SpendResources(GameSession session, Guid playerId, List<ResourceType> resources);

    (bool IsSuccess, string ErrorMessage) BuildCamp(GameSession session, Guid playerId, Guid nodeId);
    (bool IsSuccess, string ErrorMessage) BuildExplorer(GameSession session, Guid playerId, Guid nodeId);
    (bool IsSuccess, string ErrorMessage) MoveExplorer(
        GameSession session, Guid playerId, Guid sourcePathId, Guid targetPathId);
    (bool IsSuccess, string ErrorMessage) AdvanceProgress(
        GameSession session, Guid playerId, ProgressType progressType);

    bool CheckWinCondition(GameSession session, Guid playerId);
}