namespace GameServer;

public interface IPlayerService
{
    void DistributeHexResources(GameSession session, int diceValue);

    void AddResources(
        GameSession session, Guid playerId, Dictionary<ResourceType, int> resources);
    void SpendResources(
        GameSession session, Guid playerId, Dictionary<ResourceType, int> resources);
    void TradeWithBank(
        GameSession session,
        Guid playerId,
        Dictionary<ResourceType, int> resourcesToGive,
        ResourceType resourceToGet);

    bool TradeWithPlayer(
        GameSession session,
        Guid sourcePlayerId,
        Guid targetPlayerId,
        Dictionary<ResourceType, int> offeredResources,
        Dictionary<ResourceType, int> requestedResources);

    (bool IsSuccess, string ErrorMessage) ConvertExplorerToCamp(
        GameSession session, Guid playerId, AxialCoords nodeCoords);
    (bool IsSuccess, string ErrorMessage) CreateExplorer(
        GameSession session, Guid playerId, AxialCoords nodeCoords);
    (bool IsSuccess, string ErrorMessage) MoveExplorer(
        GameSession session, Guid playerId, List<AxialCoords> route);
    (bool IsSuccess, string ErrorMessage) AdvanceProgress(
        GameSession session, Guid playerId, ProgressType progressType);

    bool CheckWinCondition(GameSession session, Guid playerId);
}