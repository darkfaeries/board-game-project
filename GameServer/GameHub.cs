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

    public async Task<string> CreateSession()
    {
        var session = _store.CreateSession();

        await Groups.AddToGroupAsync(Context.ConnectionId, session.GetCode().ToString());
        return session.GetCode();
    }

    public async Task EndTurn(string code)
    {
        var turnResult = _turnService.EndTurn(code, Context.ConnectionId);

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

        await Clients.Group(code).SendAsync("TurnChanged", new
        {
            NextPlayerId = turnResult.NextPlayer.Id,
            NextPlayerUsername = turnResult.NextPlayer.Username
        });
    }

    public async Task<int> JoinSession(string code, string username)
    {
        var session = _store.GetSession(code);
        if (session == null)
        {
            _logger.LogCritical($"Failed to join session: session with id {code} doesn`t exist");
            throw new HubException("SessionId doesn`t exist");
        }

        var player = new Player
        {
            Username = username,
            ConnectionId = Context.ConnectionId
        };

        var result = session.JoinSession(player);
        if (!result)
        {
            _logger.LogCritical($"Failed to join session: player {player.Username} already exists");
            throw new HubException("Player already exists");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, code);
        await Clients.Group(code).SendAsync("PlayerCountChanged", session.GetPlayers().Count);

        return session.GetPlayers().Count;
    }

    public async Task<int> LeaveSession(string code)
    {
        var session = _store.GetSession(code);
        if (session == null)
        {
            _logger.LogCritical($"Failed to leave session: session with id {code} doesn`t exist");
            throw new HubException("SessionId doesn`t exist");
        }

        var player = session.GetPlayers()
                    .FirstOrDefault(p => p.Value.ConnectionId == Context.ConnectionId).Value
                    ?? throw new HubException("Player doesn't exist");

        var result = session.LeaveSession(player.Id);
        if (!result)
        {
            _logger.LogCritical($"Failed to leave session: player {player.Username} doesn`t exist");
            throw new HubException("Player doesn`t exist");
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, code);
        await Clients.Group(code).SendAsync("PlayerCountChanged", session.GetPlayers().Count);

        if (session.GetPlayers().Count == 0)
        {
            if (!_store.RemoveSession(code))
            {
                _logger.LogCritical($"Failed to delete session: session {code} doesn`t exist");
                throw new HubException("Session doesn`t exist");
            }
        }

        return session.GetPlayers().Count;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;

        foreach (var (code, session) in _store.GetSessions())
        {
            var player = session.GetPlayers()
                .FirstOrDefault(p => p.Value.ConnectionId == connectionId).Value;

            if (player is null) continue;

            session.LeaveSession(player.Id);
            await Groups.RemoveFromGroupAsync(connectionId, code);
            await Clients.Group(code).SendAsync("PlayerCountChanged", session.GetPlayers().Count);

            if (session.GetPlayers().Count == 0)
            {
                if (!_store.RemoveSession(code))
                {
                    _logger.LogCritical($"Failed to delete session: session {code} doesn`t exist");
                    throw new HubException("Session doesn`t exist");
                }
            }

            break;
        }

        await base.OnDisconnectedAsync(exception);
    }
}