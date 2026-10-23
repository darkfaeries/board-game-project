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
}