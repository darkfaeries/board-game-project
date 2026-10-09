namespace GameServer;

public interface ITurnService
{
    TurnResult EndTurn(string code, string connectionId);
}