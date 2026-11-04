namespace GameServer;

public sealed class GameRuleException : Exception
{
    public GameRuleException(string message) : base(message){}
}