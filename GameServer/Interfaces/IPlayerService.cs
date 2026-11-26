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

    (bool IsSuccess, string ErrorMessage) AdvanceProgress(
        GameSession session, Guid playerId, ProgressType progressType);

    bool CheckWinCondition(GameSession session, Guid playerId);
    bool CanBuildCamp(Player player);
    bool CanCreateExplorer(Player player);
    bool CanAfford(Player player, Dictionary<ResourceType, int>  cost);
    Dictionary<ResourceType, int> CalculateMoveCost(Player player, int steps);
    bool MeetsTravelRequirements(Player player, int clothingRequired, int shelterRequired);
}