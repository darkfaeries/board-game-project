using Microsoft.AspNetCore.SignalR;

namespace GameServer;

class GameHub : Hub
{
    ILogger<GameHub> _logger;
    private readonly IGameStore _store;
    private readonly ITurnService _turnService;
    private readonly IPlayerService _playerService;
    private readonly List<PlayerColor> _remainingColors = new List<PlayerColor> {
        PlayerColor.Red,
        PlayerColor.Blue,
        PlayerColor.White,
        PlayerColor.Orange
        };

    public GameHub(
        ILogger<GameHub> logger, IGameStore store, ITurnService turnService, IPlayerService playerService
        )
    {
        _logger = logger;
        _store = store;
        _turnService = turnService;
        _playerService = playerService;
    }

    public async Task<string> CreateSession()
    {
        var session = _store.CreateSession();

        await Groups.AddToGroupAsync(Context.ConnectionId, session.Code.ToString());
        return session.Code;
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
            NextPlayerUsername = turnResult.NextPlayer.Name
        });
    }

    public async Task<int> JoinSession(string code, string username = "")
    {
        var session = _store.GetSession(code);
        if (session == null)
        {
            _logger.LogCritical($"Failed to join session: session with id {code} doesn`t exist");
            throw new HubException("SessionId doesn`t exist");
        }

        var player = new Player(username, _remainingColors[0])
        {
            ConnectionId = Context.ConnectionId,
        };

        if (_remainingColors.Count > 0)
        {
            _remainingColors.RemoveAt(0);
        }
        else
        {
            _logger.LogCritical($"Failed to join session: no more colors available");
            throw new HubException("No more colors available");
        }


        var result = session.JoinSession(player);
        if (!result)
        {
            _logger.LogCritical($"Failed to join session: player {player.Name} already exists");
            throw new HubException("Player already exists");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, code);
        await Clients.Group(code).SendAsync("PlayerCountChanged", session.GetPlayers().Count());

        return session.GetPlayers().Count();
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
                    .FirstOrDefault(p => p.ConnectionId == Context.ConnectionId)
                    ?? throw new HubException("Player doesn't exist");

        var result = session.LeaveSession(player.Id);
        if (!result)
        {
            _logger.LogCritical($"Failed to leave session: player {player.Name} doesn`t exist");
            throw new HubException("Player doesn`t exist");
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, code);
        await Clients.Group(code).SendAsync("PlayerCountChanged", session.GetPlayers().Count());

        if (session.GetPlayers().Count() == 0)
        {
            if (!_store.RemoveSession(code))
            {
                _logger.LogCritical($"Failed to delete session: session {code} doesn`t exist");
                throw new HubException("Session doesn`t exist");
            }
        }

        return session.GetPlayers().Count();
    }

    public async Task RollPhase(string code, Guid playerId)
    {
        var session = _store.GetSession(code);
        if (session == null)
        {
            _logger.LogCritical($"Failed to execute Roll Phase : session with id {code} doesn`t exist");
            throw new HubException("SessionId doesn`t exist");
        }

        if (session.GetGameState().Phase != TurnPhase.Roll)
        {
            _logger.LogCritical($"Failed to execute Roll Phase : player {playerId} cannot roll dice in phase {session.GetGameState().Phase}");
            throw new HubException($"Cannot roll dice in phase {session.GetGameState().Phase}");
        }

        var player = session.GetPlayers()
                    .FirstOrDefault(p => p.ConnectionId == Context.ConnectionId)
                    ?? throw new HubException("Player doesn't exist");

        var diceRoll = session.RollDice();
        if (diceRoll == -1)
        {
            _logger.LogCritical($"Failed to roll dice: player {player.Name} cannot roll dice in phase {session.GetGameState().Phase}");
            throw new HubException($"Cannot roll dice in phase {session.GetGameState().Phase}");
        }

        if (diceRoll == 7)
        {
            return;
        }

        _playerService.DistributeHexResources(code, diceRoll);
        session.NextPhase();

        await Clients.Group(code).SendAsync("RollPhaseCompleted", new
        {
            PlayerId = playerId,
            DiceRoll = diceRoll,
        });
    }

    public async Task TradeWithPlayer(
        string code, Guid targetPlayerIdOrBank,
        Dictionary<ResourceType, int> offer
        )
    {
        var session = _store.GetSession(code);
        if (session == null)
        {
            _logger.LogCritical($"Failed to execute Trade Phase : session with id {code} doesn`t exist");
            throw new HubException("SessionId doesn`t exist");
        }

        if (session.GetGameState().Phase != TurnPhase.Trade)
        {
            _logger.LogCritical($"Failed to execute Trade Phase : player cannot trade in phase {session.GetGameState().Phase}");
            throw new HubException($"Cannot trade in phase {session.GetGameState().Phase}");
        }

        var player = session.GetPlayers()
                    .FirstOrDefault(p => p.ConnectionId == Context.ConnectionId)
                    ?? throw new HubException("Player doesn't exist");

        if (!player.Resources.CanAfford(offer))
        {
            _logger.LogCritical($"Failed to execute Trade Phase: not enough resources to spend for player {player.Name}");
            throw new HubException("Not enough resources to spend");
        }

        _playerService.AddResources(code, targetPlayerIdOrBank, offer.Keys.ToList());
        _playerService.SpendResources(code, player.Id, offer.Keys.ToList());

        session.NextPhase();

        await Clients.Group(code).SendAsync("TradePhaseCompleted", new
        {
            Source = player.Id,
            target = targetPlayerIdOrBank,
            Offer = offer
        });
    }

    public async Task TradeWithBank(
        string code,
        Dictionary<ResourceType, int> resourcesToGive,
        ResourceType resourceToGet,
        int amountToGet
    )
    {
        var session = _store.GetSession(code);
        if (session == null)
        {
            _logger.LogCritical($"Failed to execute Bank Trade : session with id {code} doesn`t exist");
            throw new HubException("SessionId doesn`t exist");
        }

        if (session.GetGameState().Phase != TurnPhase.Trade)
        {
            _logger.LogCritical($"Failed to execute Bank Trade : player cannot trade in phase {session.GetGameState().Phase}");
            throw new HubException($"Cannot trade in phase {session.GetGameState().Phase}");
        }

        var player = session.GetPlayers()
                    .FirstOrDefault(p => p.ConnectionId == Context.ConnectionId)
                    ?? throw new HubException("Player not found");

        if (!player.Resources.CanAfford(resourcesToGive))
        {
            _logger.LogCritical($"Failed to execute Bank Trade: not enough resources to give to the bank for player {player.Name}");
            throw new HubException("Not enough resources to give to the bank");
        }

        _playerService.SpendResources(code, player.Id, resourcesToGive.Keys.ToList());

        var reward = new Dictionary<ResourceType, int> { { resourceToGet, amountToGet } };
        _playerService.AddResources(code, player.Id, reward.Keys.ToList());

        session.NextPhase();

        await Clients.Group(code).SendAsync("BankTradeCompleted", new
        {
            PlayerId = player.Id,
            Given = resourcesToGive,
            Received = reward
        });
    }

    public async Task BuildCamp(string code, Guid nodeId)
    {
        var session = _store.GetSession(code);
        if (session == null)
        {
            _logger.LogCritical($"Failed to execute Build Camp : session with id {code} doesn`t exist");
            throw new HubException("SessionId doesn`t exist");
        }

        if (session.GetGameState().Phase != TurnPhase.Actions)
        {
            _logger.LogCritical($"Failed to execute Build Camp : player cannot build camp in phase {session.GetGameState().Phase}");
            throw new HubException($"Cannot build camp in phase {session.GetGameState().Phase}");
        }

        var player = session.GetPlayers()
                    .FirstOrDefault(p => p.ConnectionId == Context.ConnectionId)
                    ?? throw new HubException("Player not found");

        var result = _playerService.BuildCamp(code, player.Id, nodeId);
        if (!result.IsSuccess) throw new HubException(result.ErrorMessage);

        session.NextPhase();

        await Clients.Group(code).SendAsync("CampBuilt", new { player.Id, nodeId });
    }

    public async Task BuildExplorer(string code, Guid nodeId)
    {
        var session = _store.GetSession(code);
        if (session == null)
        {
            _logger.LogCritical($"Failed to execute Build Explorer : session with id {code} doesn`t exist");
            throw new HubException("SessionId doesn`t exist");
        }

        if (session.GetGameState().Phase != TurnPhase.Actions)
        {
            _logger.LogCritical($"Failed to execute Build Explorer : player cannot build explorer in phase {session.GetGameState().Phase}");
            throw new HubException($"Cannot build explorer in phase {session.GetGameState().Phase}");
        }

        var player = session.GetPlayers()
                    .FirstOrDefault(p => p.ConnectionId == Context.ConnectionId)
                    ?? throw new HubException("Player not found");

        var result = _playerService.BuildExplorer(code, player.Id, nodeId);
        if (!result.IsSuccess) throw new HubException(result.ErrorMessage);

        session.NextPhase();

        await Clients.Group(code).SendAsync("ExplorerBuilt", new { player.Id, nodeId });
    }

    public async Task MoveExplorer(string code, Guid sourcePathId, Guid targetPathId)
    {
        var session = _store.GetSession(code);
        if (session == null)
        {
            _logger.LogCritical($"Failed to execute Move Explorer : session with id {code} doesn`t exist");
            throw new HubException("SessionId doesn`t exist");
        }

        if (session.GetGameState().Phase != TurnPhase.Actions)
        {
            _logger.LogCritical($"Failed to execute Move Explorer : player cannot move explorer in phase {session.GetGameState().Phase}");
            throw new HubException($"Cannot move explorer in phase {session.GetGameState().Phase}");
        }

        var player = session.GetPlayers()
                    .FirstOrDefault(p => p.ConnectionId == Context.ConnectionId)
                    ?? throw new HubException("Player not found");

        var result = _playerService.MoveExplorer(code, player.Id, sourcePathId, targetPathId);
        if (!result.IsSuccess) throw new HubException(result.ErrorMessage);

        session.NextPhase();

        await Clients.Group(code).SendAsync("ExplorerMoved", new { player.Id, sourcePathId, targetPathId });
    }

    public async Task AdvanceProgress(string code, ProgressType progressType)
    {
        var session = _store.GetSession(code);
        if (session == null)
        {
            _logger.LogCritical($"Failed to execute Advance Progress : session with id {code} doesn`t exist");
            throw new HubException("SessionId doesn`t exist");
        }

        if (session.GetGameState().Phase != TurnPhase.Actions)
        {
            _logger.LogCritical($"Failed to execute Advance Progress : player cannot advance progress in phase {session.GetGameState().Phase}");
            throw new HubException($"Cannot advance progress in phase {session.GetGameState().Phase}");
        }

        var player = session.GetPlayers()
                    .FirstOrDefault(p => p.ConnectionId == Context.ConnectionId)
                    ?? throw new HubException("Player not found");

        var result = _playerService.AdvanceProgress(code, player.Id, progressType);
        if (!result.IsSuccess) throw new HubException(result.ErrorMessage);

        session.NextPhase();

        await Clients.Group(code).SendAsync("ProgressAdvanced", new { player.Id, progressType });
    }

    public async Task CheckWinner(string code)
    {
        var session = _store.GetSession(code);
        if (session == null)
        {
            _logger.LogCritical($"Failed to execute Check Winner : session with id {code} doesn`t exist");
            throw new HubException("SessionId doesn`t exist");
        }

        var player = session.GetPlayers()
                    .FirstOrDefault(p => p.ConnectionId == Context.ConnectionId)
                    ?? throw new HubException("Player not found");

        var isWinner = _playerService.CheckWinCondition(code, player.Id);

        if (isWinner)
        {
            await Clients.Group(code).SendAsync("PlayerWon", new { player.Id });
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;

        foreach (var (code, session) in _store.GetSessions())
        {
            var player = session.GetPlayers()
                .FirstOrDefault(p => p.ConnectionId == connectionId);

            if (player is null) continue;

            session.LeaveSession(player.Id);
            await Groups.RemoveFromGroupAsync(connectionId, code);
            await Clients.Group(code).SendAsync("PlayerCountChanged", session.GetPlayers().Count());

            if (session.GetPlayers().Count() == 0)
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