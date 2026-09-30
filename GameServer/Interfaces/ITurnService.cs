namespace GameServer;

public interface ITurnService
{
    TurnResult EndTurn(Guid sessionId, string connectionId);
}