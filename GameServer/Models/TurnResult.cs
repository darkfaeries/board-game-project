namespace GameServer;

public record TurnResult(bool Result, string Message, Player? NextPlayer = null);