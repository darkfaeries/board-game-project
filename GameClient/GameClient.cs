using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace GameClient;

public class GameClient : IAsyncDisposable
{
    private readonly string _baseUrl;
    private readonly ILogger<GameClient> _logger;
    private HubConnection? _hub;
    private bool _hasJoinedSession;
    private string? _accessToken;

    public string? GameCode { get; private set; }
    public string? AuthenticatedUsername { get; private set; }
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
    public event Action<PlayerReconnectedEvent>? PlayerReconnected;
    public event Action<InitialPlacementSubmittedEvent>? InitialPlacementSubmitted;


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

    public async Task<(bool Ok, string? Error)> AuthenticateGuestAsync(string username)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken))
            return (true, null);

        try
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                string guestName = attempt == 0
                    ? username
                    : $"{username}-{Guid.NewGuid():N}";
                string password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                using var response = await Http.PostAsJsonAsync(
                    $"{_baseUrl}/api/auth/register",
                    new { Username = guestName, Password = password });
                string responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    if (responseBody.Contains("Username already exists", StringComparison.OrdinalIgnoreCase))
                        continue;

                    return (false, $"Не удалось создать гостевого пользователя: {responseBody}");
                }

                string tokenResponse = responseBody.Trim();
                _accessToken = tokenResponse.StartsWith('"')
                    ? JsonSerializer.Deserialize<string>(tokenResponse)
                    : tokenResponse;
                if (string.IsNullOrWhiteSpace(_accessToken))
                    return (false, "Сервер не вернул JWT-токен.");

                AuthenticatedUsername = guestName;
                return (true, null);
            }

            return (false, "Не удалось подобрать свободное имя гостя. Попробуйте ещё раз.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register guest user");
            return (false, $"Не удалось авторизоваться: {ex.Message}");
        }
    }

    // Локальный сервер не должен ходить через системный прокси.
    private static readonly HttpClient Http = new(new HttpClientHandler { UseProxy = false });

    /// <summary>
    /// Забирает состояние игроков сессии по уже существующему REST-эндпоинту
    /// GET /api/sessions/{code}/players. null — если забрать не удалось.
    /// </summary>
    public async Task<GameSnapshot?> GetSnapshotAsync()
    {
        if (string.IsNullOrWhiteSpace(GameCode))
            return null;

        try
        {
            if (string.IsNullOrWhiteSpace(_accessToken))
                return null;

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/api/sessions/{GameCode}/players");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            using var response = await Http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync();
            return GameSnapshotParser.ParsePlayers(json, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch session players");
            return null;
        }
    }

    public async Task<(bool IsSuccess, string? Error)> ConnectAsync(string gameCode, string username = "Player")
    {
        if (string.IsNullOrWhiteSpace(gameCode))
        {
            return (false, "Game code cannot be empty.");
        }

        try
        {
            var auth = await AuthenticateGuestAsync(username);
            if (!auth.Ok)
                return (false, auth.Error);

            await EnsureConnectedAsync();

            var code = gameCode.Trim().ToUpperInvariant();
            var players = await _hub!.InvokeAsync<int>("JoinSession", code);

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
    
    public Task<(bool Ok, string? Error)> SubmitInitialPlacementAsync(AxialCoords nodeCoords) =>
        InvokeGameMethodAsync("SubmitInitialPlacement", false, nodeCoords);

    public Task<(bool Ok, string? Error)> ReconnectAsync(string gameCode) =>
        InvokeGameMethodAsync("ReconnectToSession", false, gameCode);

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

        if (string.IsNullOrWhiteSpace(_accessToken))
            throw new InvalidOperationException("Authenticate the guest user before connecting to SignalR.");

        if (_hub is not null)
            await DisconnectAsync();

        var hubUrl = _baseUrl.EndsWith("/gamehub", StringComparison.OrdinalIgnoreCase)
            ? _baseUrl
            : $"{_baseUrl}/gamehub";

        _hub = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(_accessToken);
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
        _hub.On<PlayerReconnectedEvent>("PlayerReconnected", value => PlayerReconnected?.Invoke(value));

        _hub.On<InitialPlacementSubmittedEvent>(
            "InitialPlacementSubmitted",
            value => InitialPlacementSubmitted?.Invoke(value));
        
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

            // Вызываем через перегрузки InvokeAsync с позиционными аргументами:
            // InvokeAsync(method, argsArray) принял бы массив за один аргумент,
            // а InvokeCoreAsync с typeof(void) падает на разборе пустого ответа.
            await InvokeHubMethodAsync(methodName, invocationArgs);

            if (clearSessionOnSuccess)
                ClearSession();

            return (true, null);
        }
        catch (HubException ex)
        {
            // Сервер отклонил действие (неверная фаза, не тот игрок) — сессию не теряем.
            _logger.LogWarning(ex, "Hub rejected {Method}", methodName);
            return (false, $"Hub Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    private Task InvokeHubMethodAsync(string methodName, object?[] args) => args.Length switch
    {
        1 => _hub!.InvokeAsync(methodName, args[0]),
        2 => _hub!.InvokeAsync(methodName, args[0], args[1]),
        3 => _hub!.InvokeAsync(methodName, args[0], args[1], args[2]),
        4 => _hub!.InvokeAsync(methodName, args[0], args[1], args[2], args[3]),
        _ => throw new InvalidOperationException(
            $"Unsupported argument count {args.Length} for hub method {methodName}."),
    };

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
public sealed record PlayerReconnectedEvent(string code);
public sealed record InitialPlacementSubmittedEvent(
    Guid PlayerId,
    AxialCoords NodeCoords,
    Guid? NextPlayerId,
    GameStatus Status,
    TurnPhase Phase);
