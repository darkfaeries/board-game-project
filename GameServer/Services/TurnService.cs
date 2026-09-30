namespace GameServer;

public class TurnService : ITurnService
{
    private readonly IGameStore _gameStore;
    private ILogger<TurnService> _logger;

    public TurnService(IGameStore gameStore, ILogger<TurnService> logger)
    {
        _gameStore = gameStore;
        _logger = logger;
    }

    public TurnResult EndTurn(Guid sessionId, string connectionId)
    {
        var session = _gameStore.GetSession(sessionId.ToString());
        if (session == null)
        {
            _logger.LogWarning($"Session with ID {sessionId} not found.");
            return new TurnResult(false, $"Session with ID {sessionId} not found.");
        }

        var player = session.GetPlayers().FirstOrDefault(p => p.Value.ConnectionId == connectionId).Value;
        if (player == null)
        {
            _logger.LogWarning($"Player with connection ID {connectionId} not found in session {sessionId}.");
            return new TurnResult(false, $"Player with connection ID {connectionId} not found in session {sessionId}.");
        }

        if (session.GetCurrentPlayer().Id != player.Id)
        {
            _logger.LogWarning($"Player {player.Username} attempted to end turn out of order in session {sessionId}.");
            return new TurnResult(false, $"It's not player {player.Username}'s turn.");
        }

        session.NextPlayer();
        return new TurnResult(true, "Ok", session.GetCurrentPlayer());
    }
}