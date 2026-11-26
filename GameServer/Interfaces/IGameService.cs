namespace GameServer;

public interface IGameService
{
    bool StartGame(GameSession session);
    bool SubmitInitialPlacement(GameSession session, Guid playerId, AxialCoords nodeCoords);
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
    void ConvertExplorerToCamp(GameSession session, Guid playerId, AxialCoords nodeCoords);
    void CreateExplorer(GameSession session, Guid playerId, AxialCoords nodeCoords);
    void MoveExplorer(GameSession session, Guid playerId, List<AxialCoords> route);
    bool SubmitTradeDecision(
        GameSession session,
        Guid tradeId, 
        bool isAccepted);
}