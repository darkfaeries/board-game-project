namespace GameServer;

public interface IGameService
{
    bool StartGame(GameSession session);
    void OnSevenRolled(GameSession session);
    public bool SubmitDiscardResources(
        GameSession session,
        Guid playerId,
        Dictionary<ResourceType, int> selectedResources);
}