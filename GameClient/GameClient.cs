using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text.Json;

namespace GameClient;

public class GameClient : IAsyncDisposable
{
    private readonly string _baseUrl;
    private readonly ILogger<GameClient> _logger;
    private HubConnection? _hub;
    private bool _hasJoinedSession;

    public string? GameCode { get; private set; }
    public int PlayerCount { get; private set; }
    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public event Action<int>? PlayerCountChanged;
    public event Action<List<PlayerDto>>? PlayerListChanged;
    public event Action<GameStartedEvent>? GameStarted;
    public event Action<TurnChangedEvent>? TurnChanged;
    public event Action<SevenRolledEvent>? SevenRolled;
    public event Action<DiscardResourcesSubmittedEvent>? DiscardResourcesSubmitted;
    public event Action<RollPhaseCompletedEvent>? RollPhaseCompleted;
    public event Action<PlayerTradePhaseCompletedEvent>? PlayerTradePhaseCompleted;
    public event Action<BankTradeCompletedEvent>? BankTradeCompleted;
    public event Action<ExplorerCreatedEvent>? ExplorerCreated;
    public event Action<ExplorerMovedEvent>? ExplorerMoved;
    public event Action<ExplorerConvertedToCampEvent>? ExplorerConvertedToCamp;
    public event Action<ProgressAdvancedEvent>? ProgressAdvanced;
    public event Action<WinnerCheckedEvent>? WinnerChecked;


    public GameClient(string baseUrl, ILogger<GameClient> logger)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _logger = logger;
    }

    public async Task<(bool Ok, string? Error)> CreateSessionAsync()
    {
        try
        {
            await EnsureConnectedAsync();

            var code = await _hub!.InvokeAsync<string>("CreateSession");
            GameCode = code;
            _hasJoinedSession = false;
            return (true, null);
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    public Task<(bool Ok, string? Error)> StartGameAsync() =>
        InvokeGameMethodAsync("StartGame", false);

    public async Task<(bool IsSuccess, string? Error)> ConnectAsync(string gameCode, string username = "Player")
    {
        if (string.IsNullOrWhiteSpace(gameCode))
        {
            return (false, "Game code cannot be empty.");
        }

        try
        {
            await EnsureConnectedAsync();

            var code = gameCode.Trim().ToUpperInvariant();
            var players = await _hub!.InvokeAsync<int>("JoinSession", code, username);

            GameCode = code;
            PlayerCount = players;
            _hasJoinedSession = true;
            return (true, null);
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    // Совместимость с UI клиента: создание сессии возвращает её код в Data.
    public async Task<(bool IsSuccess, string? Data)> CreateAsync()
    {
        var result = await CreateSessionAsync();
        return (result.Ok, result.Ok ? GameCode : result.Error);
    }

    public Task<(bool Ok, string? Error)> LeaveSessionAsync() =>
        InvokeGameMethodAsync("LeaveSession", true);

    public Task<(bool Ok, string? Error)> RollPhaseAsync(Guid playerId) =>
        InvokeGameMethodAsync("RollPhase", false, playerId);

    public Task<(bool Ok, string? Error)> DiscardResourcesDecisionAsync(
        Guid playerId, Dictionary<ResourceType, int> selectedResources) =>
        InvokeGameMethodAsync("DiscardResourcesDecision", false, selectedResources);

    public Task<(bool Ok, string? Error)> TradeWithPlayerAsync(
        Guid targetPlayerId, Dictionary<ResourceType, int> offer) =>
        InvokeGameMethodAsync("TradeWithPlayer", false, targetPlayerId, offer);

    public Task<(bool Ok, string? Error)> TradeWithBankAsync(
        Dictionary<ResourceType, int> resourcesToGive,
        ResourceType resourceToGet) =>
        InvokeGameMethodAsync("TradeWithBank", false, resourcesToGive, resourceToGet);

    public Task<(bool Ok, string? Error)> ConvertExplorerToCampAsync(AxialCoords nodeCoords) =>
        InvokeGameMethodAsync("ConvertExplorerToCamp", false, nodeCoords);

    public Task<(bool Ok, string? Error)> CreateExplorerAsync(AxialCoords nodeCoords) =>
        InvokeGameMethodAsync("CreateExplorer", false, nodeCoords);

    public Task<(bool Ok, string? Error)> MoveExplorerAsync(List<AxialCoords> route) =>
        InvokeGameMethodAsync("MoveExplorer", false, route);

    public Task<(bool Ok, string? Error)> AdvanceProgressAsync(ProgressType progressType) =>
        InvokeGameMethodAsync("AdvanceProgress", false, progressType);

    public Task<(bool Ok, string? Error)> CheckWinnerAsync() =>
        InvokeGameMethodAsync("CheckWinner", false);

    public Task<(bool Ok, string? Error)> EndTurnAsync() =>
        InvokeGameMethodAsync("EndTurn", false);

    public async Task DisconnectAsync()
    {
        if (_hub is null)
            return;

        try
        {
            if (_hasJoinedSession && !string.IsNullOrWhiteSpace(GameCode) && IsConnected)
                await _hub!.InvokeAsync("LeaveSession", GameCode);
            await _hub.StopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error occurred while disconnecting from the hub.");
        }
        finally
        {
            await _hub.DisposeAsync();
            _hub = null;
            ClearSession();
        }
    }

    private async Task EnsureConnectedAsync()
    {
        if (_hub?.State == HubConnectionState.Connected)
            return;

        if (_hub is not null)
            await DisconnectAsync();

        var hubUrl = _baseUrl.EndsWith("/gamehub", StringComparison.OrdinalIgnoreCase)
            ? _baseUrl
            : $"{_baseUrl}/gamehub";

        _hub = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = handler =>
                {
                    if (handler is HttpClientHandler httpClientHandler)
                        httpClientHandler.UseProxy = false;
                    return handler;
                };
            })
            .WithAutomaticReconnect()
            .Build();

        _hub.On<JsonElement>("PlayerCountChanged", payload =>
        {
            if (payload.ValueKind == JsonValueKind.Array)
            {
                var players = payload.Deserialize<List<PlayerDto>>() ?? new List<PlayerDto>();
                PlayerCount = players.Count;
                PlayerListChanged?.Invoke(players);
            }
            else if (payload.ValueKind == JsonValueKind.Number && payload.TryGetInt32(out var count))
            {
                PlayerCount = count;
            }
            else
            {
                _logger.LogWarning("Received an unsupported PlayerCountChanged payload.");
                return;
            }
            PlayerCountChanged?.Invoke(PlayerCount);
        });
        _hub.On<GameStartedEvent>("GameStarted", value => GameStarted?.Invoke(value));
        _hub.On<TurnChangedEvent>("TurnChanged", value => TurnChanged?.Invoke(value));
        _hub.On<SevenRolledEvent>("SevenRolled", value => SevenRolled?.Invoke(value));
        _hub.On<DiscardResourcesSubmittedEvent>(
            "DiscardResourcesSubmitted", 
            value => DiscardResourcesSubmitted?.Invoke(value));
        _hub.On<RollPhaseCompletedEvent>("RollPhaseCompleted", value => RollPhaseCompleted?.Invoke(value));
        _hub.On<PlayerTradePhaseCompletedEvent>(
            "PlayerTradePhaseCompleted",
            value => PlayerTradePhaseCompleted?.Invoke(value));
        _hub.On<BankTradeCompletedEvent>("BankTradeCompleted", value => BankTradeCompleted?.Invoke(value));
        _hub.On<ExplorerCreatedEvent>("ExplorerCreated", value => ExplorerCreated?.Invoke(value));
        _hub.On<ExplorerMovedEvent>("ExplorerMoved", value => ExplorerMoved?.Invoke(value));
        _hub.On<ExplorerConvertedToCampEvent>(
            "ExplorerConvertedToCamp",
            value => ExplorerConvertedToCamp?.Invoke(value));
        _hub.On<ProgressAdvancedEvent>("ProgressAdvanced", value => ProgressAdvanced?.Invoke(value));
        _hub.On<WinnerCheckedEvent>("WinnerChecked", value => WinnerChecked?.Invoke(value));

        await _hub.StartAsync();
    }

    private async Task<(bool Ok, string? Error)> InvokeGameMethodAsync(
        string methodName,
        bool clearSessionOnSuccess,
        params object[] args)
    {
        if (string.IsNullOrWhiteSpace(GameCode))
        {
            ClearSession();
            return (false, "You are not connected to a session.");
        }

        try
        {
            await EnsureConnectedAsync();

            var invocationArgs = new object[args.Length + 1];
            invocationArgs[0] = GameCode!;
            Array.Copy(args, 0, invocationArgs, 1, args.Length);

            await _hub!.InvokeAsync(methodName, invocationArgs);

            if (clearSessionOnSuccess)
                ClearSession();

            return (true, null);
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    private (bool Ok, string? Error) Fail(Exception ex)
    {
        ClearSession();
        _logger.LogError(ex, "Error occurred in the client");

        var message = ex switch
        {
            HubException => $"Hub Error: {ex.Message}",
            HttpRequestException => $"Network Error: {ex.Message}",
            _ => $"Unexpected Error: {ex.Message}",
        };

        return (false, message);
    }

    private void ClearSession()
    {
        GameCode = null;
        PlayerCount = 0;
        _hasJoinedSession = false;
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}

public sealed record PlayerDto(Guid Id, string Name);
public sealed record GameStartedEvent(Guid? CurrentPlayerId, TurnPhase CurrentPhase);
public sealed record TurnChangedEvent(Guid NextPlayerId, string NextPlayerUsername);
public sealed record RollPhaseCompletedEvent(Guid PlayerId, int DiceRoll);
public sealed record PlayerTradePhaseCompletedEvent(
    Guid Source,
    Guid Target,
    Dictionary<ResourceType, int> Offer);

public sealed record BankTradeCompletedEvent(
    Guid PlayerId,
    Dictionary<ResourceType, int> Given,
    Dictionary<ResourceType, int> Received);

public sealed record ExplorerCreatedEvent(Guid Id, AxialCoords NodeCoords);
public sealed record ExplorerMovedEvent(Guid Id, List<AxialCoords> Route);
public sealed record ExplorerConvertedToCampEvent(Guid Id, AxialCoords NodeCoords);
public sealed record ProgressAdvancedEvent(Guid Id, ProgressType ProgressType);
public sealed record WinnerCheckedEvent(Guid Id);
public sealed record SevenRolledEvent(Guid Id, int DiceRoll, List<PendingDecisionEvent> PendingDecisions);
public sealed record PendingDecisionEvent(Guid PlayerId, string DecisionType, int RequiredCount);
public sealed record DiscardResourcesSubmittedEvent(Guid Id);