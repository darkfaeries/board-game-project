namespace GameServer;

public interface ITurnService
{
    TurnResult EndTurn(GameSession session, Guid playerId);
}