using Microsoft.AspNetCore.SignalR;

namespace GameServer;
class GameHub : Hub
{
    ILogger<GameHub> _logger;
    private readonly IGameStore _store;
    private readonly ITurnService _turnService;

    public GameHub(ILogger<GameHub> logger, IGameStore store, ITurnService turnService)
    {
        _logger = logger;
        _store = store;
        _turnService = turnService;
    }

    public async Task EndTurn(Guid sessionId)
    {
        var turnResult = _turnService.EndTurn(sessionId, Context.ConnectionId);

        if (!turnResult.Result)
        {
            _logger.LogCritical($"End turn failed with message {turnResult.Message}");
            throw new HubException($"{turnResult.Message}");
        }

        if (turnResult.NextPlayer == null)
        {
            _logger.LogCritical($"End turn failed: player can`t be null");
            throw new HubException($"End turn failed: player can`t be null");
        }

        await Clients.Group(sessionId.ToString()).SendAsync("TurnChanged", new 
        {
            NextPlayerId = turnResult.NextPlayer.Id,
            NextPlayerUsername = turnResult.NextPlayer.Username
        });
    }

    public async Task JoinSession(Guid sessionId, Player player)
    {
        var session = _store.GetSession(sessionId.ToString());
        if (session == null)
        {
            _logger.LogCritical($"Failed to join session: session with id {sessionId} doesn`t exist");
            throw new HubException("SessionId doesn`t exist");
        }

        var result = session.JoinSession(sessionId, player); // ?!
        if (!result)
        {
            _logger.LogCritical($"Failed to join session: player {player.Username} already exists");
            throw new HubException("Player already exists");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId.ToString());
        await Clients.Group(sessionId.ToString()).SendAsync("PlayerJoined", player);
    }
}