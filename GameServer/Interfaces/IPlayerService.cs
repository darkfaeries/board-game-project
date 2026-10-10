namespace GameServer;

public interface IPlayerService
{
    void DistributeHexResources(string code, int diceValue);

    void AddResources(string code, Guid playerId, List<ResourceType> resources);
    void SpendResources(string code, Guid playerId, List<ResourceType> resources);

    (bool IsSuccess, string ErrorMessage) BuildCamp(string code, Guid playerId, Guid nodeId);
    (bool IsSuccess, string ErrorMessage) BuildExplorer(string code, Guid playerId, Guid nodeId);
    (bool IsSuccess, string ErrorMessage) MoveExplorer(string code, Guid playerId, Guid sourcePathId, Guid targetPathId);
    (bool IsSuccess, string ErrorMessage) AdvanceProgress(string code, Guid playerId, ProgressType progressType);

    bool CheckWinCondition(string code, Guid playerId);
}