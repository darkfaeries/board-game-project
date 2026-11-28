namespace GameServer;

public interface IGameService
{
    bool StartGame(GameSession session);
    void OnSevenRolled(GameSession session);
    Guid TradeWithPlayer(
        GameSession session,
        Guid sourcePlayerId,
        Guid targetPlayerId,
        Dictionary<ResourceType, int> offeredResources,
        Dictionary<ResourceType, int> requestedResources);
    bool SubmitDiscardResources(
        GameSession session,
        Guid playerId,
        Dictionary<ResourceType, int> selectedResources);
    bool SubmitTradeDecision(
        GameSession session,
        Guid tradeId, 
        bool isAccepted);
}