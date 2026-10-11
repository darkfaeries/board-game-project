using Microsoft.AspNetCore.SignalR;

namespace GameServer;

class GameHub : Hub
{
    ILogger<GameHub> _logger;
    private readonly IGameStore _store;
    private readonly ITurnService _turnService;
    private readonly IPlayerService _playerService;

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
        var session = _store.GetSession(code);
        if (session == null)
        {
            _logger.LogCritical($"Failed to join session: session with id {code} doesn`t exist");
            throw new HubException("SessionId doesn`t exist");
        }

        var player = session.GetPlayers()
                    .FirstOrDefault(p => p.ConnectionId == Context.ConnectionId)
                    ?? throw new HubException("Player doesn't exist");

        var turnResult = _turnService.EndTurn(session, player.Id);

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

        var player = new Player(username, PlayerColor.White)
        {
            ConnectionId = Context.ConnectionId,
        };

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
        EnsureCurrentPlayer(session, player);
        if (playerId != player.Id)
        {
            _logger.LogWarning($"Player {player.Id} supplied another player ID for rolling.");
            throw new HubException("Invalid player ID.");
        }

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

        _playerService.DistributeHexResources(session, diceRoll);

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
        EnsureCurrentPlayer(session, player);

        if (targetPlayerIdOrBank == player.Id)
        {
            _logger.LogWarning($"Player {player.Id} attempted to trade with themselves.");
            throw new HubException("You cannot trade with yourself.");
        }

        if (session.GetGameState().GetPlayer(targetPlayerIdOrBank) == null)
        {
            _logger.LogWarning($"Trade target {targetPlayerIdOrBank} was not found.");
            throw new HubException("Target player was not found.");
        }

        if (!player.Resources.CanAfford(offer))
        {
            _logger.LogCritical($"Failed to execute Trade Phase: not enough resources to spend for player {player.Name}");
            throw new HubException("Not enough resources to spend");
        }

        _playerService.AddResources(session, targetPlayerIdOrBank, offer);
        _playerService.SpendResources(session, player.Id, offer);

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
        EnsureCurrentPlayer(session, player);

        var reward = new Dictionary<ResourceType, int> { { resourceToGet, amountToGet } };
        try
        {
            _playerService.TradeWithBank(
                session, player.Id, resourcesToGive, resourceToGet, amountToGet);
        }
        catch (ArgumentException)
        {
            _logger.LogWarning($"Invalid bank trade requested by player {player.Id}.");
            throw new HubException("Invalid bank trade.");
        }
        catch (InvalidOperationException)
        {
            _logger.LogWarning($"Player {player.Id} cannot afford the requested bank trade.");
            throw new HubException("Not enough resources to give to the bank.");
        }

        await Clients.Group(code).SendAsync("BankTradeCompleted", new
        {
            PlayerId = player.Id,
            Given = resourcesToGive,
            Received = reward
        });
    }

    public async Task ConvertExplorerToCamp(string code, AxialCoords nodeCoords)
    {
        var session = _store.GetSession(code);
        if (session == null)
        {
            _logger.LogCritical($"Failed to execute Convert Explorer To Camp : session with id {code} doesn`t exist");
            throw new HubException("SessionId doesn`t exist");
        }

        if (session.GetGameState().Phase != TurnPhase.Actions)
        {
            _logger.LogCritical($"Failed to execute Convert Explorer To Camp : player cannot build camp in phase {session.GetGameState().Phase}");
            throw new HubException($"Cannot build camp in phase {session.GetGameState().Phase}");
        }

        var player = session.GetPlayers()
                    .FirstOrDefault(p => p.ConnectionId == Context.ConnectionId)
                    ?? throw new HubException("Player not found");
        EnsureCurrentPlayer(session, player);

        var result = _playerService.ConvertExplorerToCamp(session, player.Id, nodeCoords);
        if (!result.IsSuccess) throw new HubException(result.ErrorMessage);

        await Clients.Group(code).SendAsync("ExplorerConvertedToCamp", new { player.Id, nodeCoords });
    }

    public async Task BuildExplorer(string code, AxialCoords nodeCoords)
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
        EnsureCurrentPlayer(session, player);

        var result = _playerService.CreateExplorer(session, player.Id, nodeCoords);
        if (!result.IsSuccess) throw new HubException(result.ErrorMessage);

        await Clients.Group(code).SendAsync("ExplorerBuilt", new { player.Id, nodeCoords });
    }

    public async Task MoveExplorer(string code, List<AxialCoords> route)
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
        EnsureCurrentPlayer(session, player);

        var result = _playerService.MoveExplorer(session, player.Id, route);
        if (!result.IsSuccess) throw new HubException(result.ErrorMessage);

        await Clients.Group(code).SendAsync("ExplorerMoved", new { player.Id, route });
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
        EnsureCurrentPlayer(session, player);

        var result = _playerService.AdvanceProgress(session, player.Id, progressType);
        if (!result.IsSuccess) throw new HubException(result.ErrorMessage);

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
        EnsureCurrentPlayer(session, player);

        var isWinner = _playerService.CheckWinCondition(session, player.Id);

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

    private void EnsureCurrentPlayer(GameSession session, Player player)
    {
        if (session.GetGameState().CurrentPlayerId != player.Id)
        {
            _logger.LogWarning(
                $"Player {player.Id} attempted an action out of turn in session {session.Code}.");
            throw new HubException("It is not your turn.");
        }
    }
}