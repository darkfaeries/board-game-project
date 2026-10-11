namespace GameServer;

public class TurnService : ITurnService
{
    private ILogger<TurnService> _logger;

    public TurnService(ILogger<TurnService> logger)
    {
        _logger = logger;
    }

    public TurnResult EndTurn(GameSession session, Guid playerId)
    {
        //var session = _gameStore.GetSession(code);
        if (session == null)
        {
            _logger.LogWarning($"Session not found.");
            return new TurnResult(false, $"Session not found.");
        }

        var player = session.GetPlayers().FirstOrDefault(p => p.Id == playerId);
        if (player == null)
        {
            _logger.LogWarning($"Player with ID {playerId} not found in session {session.Code}.");
            return new TurnResult(false, $"Player with ID {playerId} not found in session {session.Code}.");
        }

        if (session.GetGameState().CurrentPlayerId != player.Id)
        {
            _logger.LogWarning($"Player {player.Name} attempted to end turn out of order in session {session.Code}.");
            return new TurnResult(false, $"It's not player {player.Name}'s turn.");
        }

        if (session.NextPhase() == TurnPhase.Roll)
        {
            session.GetGameState().Phase = TurnPhase.Roll;
            session.NextPlayer();
        }

        return new TurnResult(true, "Ok", session.GetGameState().GetCurrentPlayer());
    }
}