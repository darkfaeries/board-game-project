using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace GameClient;

public class GameClient
{
    private readonly string _baseUrl;
    private readonly ILogger<GameClient> _logger;
    private HubConnection? _hub;

    public string? GameCode { get; private set; }
    public int PlayerCount { get; private set; }
    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public event Action<int>? PlayerCountChanged;

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
            return (true, null);
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    public async Task<(bool Ok, string? Error)> ConnectAsync(string gameCode)
    {
        if (string.IsNullOrWhiteSpace(gameCode))
        {
            ClearSession();
            return (false, "Game code cannot be empty.");
        }

        //пока нет авторизации будет рандомыш
        var tempname = $"Player_{Guid.NewGuid().ToString().Substring(0, 4)}"; 

        try
        {
            await EnsureConnectedAsync();

            var code = gameCode.Trim().ToUpperInvariant();
            var players = await _hub!.InvokeAsync<int>("JoinSession", code, tempname);

            GameCode = code;
            PlayerCount = players;
            return (true, null);
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    public async Task<(bool Ok, string? Error)> LeaveSessionAsync()
    {
        if (string.IsNullOrWhiteSpace(GameCode))
        {
            ClearSession();
            return (false, "You are not connected to a session.");
        }

        try
        {
            await EnsureConnectedAsync();

            var code = GameCode!;
            await _hub!.InvokeAsync("LeaveSession", code);

            ClearSession();
            return (true, null);
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    public async Task<(bool Ok, string? Error)> RollPhaseAsync(Guid playerId)    
    {
        if (string.IsNullOrWhiteSpace(GameCode))
        {
            ClearSession();
            return (false, "You are not connected to a session.");
        }

        try
        {
            await EnsureConnectedAsync();

            var code = GameCode!;
            await _hub!.InvokeAsync("RollPhase", code, playerId);

            return (true, null);
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    public async Task<(bool Ok, string? Error)> TradeWithPlayerAsync(
        Guid targetPlayerId, Dictionary<ResourceType, int> offer)
    {
        if (string.IsNullOrWhiteSpace(GameCode))
        {
            ClearSession();
            return (false, "You are not connected to a session.");
        }

        try
        {
            await EnsureConnectedAsync();

            var code = GameCode!;
            await _hub!.InvokeAsync("TradeWithPlayer", code, targetPlayerId, offer);

            return (true, null);
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    public async Task<(bool Ok, string? Error)> TradeWithBankAsync(
        Dictionary<ResourceType, int> resourcesToGive,
        ResourceType resourceToGet)
    {
        if (string.IsNullOrWhiteSpace(GameCode))
        {
            ClearSession();
            return (false, "You are not connected to a session.");
        }

        try
        {
            await EnsureConnectedAsync();

            var code = GameCode!;
            await _hub!.InvokeAsync("TradeWithBank", code, resourcesToGive, resourceToGet);

            return (true, null);
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    public async Task<(bool Ok, string? Error)> ConvertExplorerToCampAsync(AxialCoords nodeCoords)
    {
        if (string.IsNullOrWhiteSpace(GameCode))
        {
            ClearSession();
            return (false, "You are not connected to a session.");
        }

        try
        {
            await EnsureConnectedAsync();

            var code = GameCode!;
            await _hub!.InvokeAsync("ConvertExplorerToCamp", code, nodeCoords);

            return (true, null);
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    public async Task<(bool Ok, string? Error)> CreateExplorerAsync(AxialCoords nodeCoords)
    {
        if (string.IsNullOrWhiteSpace(GameCode))
        {
            ClearSession();
            return (false, "You are not connected to a session.");
        }

        try
        {
            await EnsureConnectedAsync();

            var code = GameCode!;
            await _hub!.InvokeAsync("CreateExplorer", code, nodeCoords);

            return (true, null);
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    public async Task<(bool Ok, string? Error)> MoveExplorerAsync(List<AxialCoords> route)
    {
        if (string.IsNullOrWhiteSpace(GameCode))
        {
            ClearSession();
            return (false, "You are not connected to a session.");
        }

        try
        {
            await EnsureConnectedAsync();

            var code = GameCode!;
            await _hub!.InvokeAsync("MoveExplorer", code, route);

            return (true, null);
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    public async Task<(bool Ok, string? Error)> AdvanceProgressAsync(ProgressType progressType)
    {
        if (string.IsNullOrWhiteSpace(GameCode))
        {
            ClearSession();
            return (false, "You are not connected to a session.");
        }

        try
        {
            await EnsureConnectedAsync();

            var code = GameCode!;
            await _hub!.InvokeAsync("AdvanceProgress", code, progressType);

            return (true, null);
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    public async Task<(bool Ok, string? Error)> CheckWinnerAsync()
    {
        if (string.IsNullOrWhiteSpace(GameCode))
        {
            ClearSession();
            return (false, "You are not connected to a session.");
        }

        try
        {
            await EnsureConnectedAsync();

            var code = GameCode!;
            await _hub!.InvokeAsync("CheckWinner", code);

            return (true, null);
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    public async Task<(bool Ok, string? Error)> EndTurnAsync()
    {
        if (string.IsNullOrWhiteSpace(GameCode))
        {
            ClearSession();
            return (false, "You are not connected to a session.");
        }

        try
        {
            await EnsureConnectedAsync();

            var code = GameCode!;
            await _hub!.InvokeAsync("EndTurn", code);

            return (true, null);
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    public async Task DisconnectAsync()
    {
        if (_hub is null)
            return;

        try
        {
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

        _hub = new HubConnectionBuilder()
            .WithUrl($"{_baseUrl}/gamehub")
            .WithAutomaticReconnect()
            .Build();

        _hub.On<int>("PlayerCountChanged", count =>
        {
            PlayerCount = count;
            PlayerCountChanged?.Invoke(count);
        });

        await _hub.StartAsync();
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
    }
}
