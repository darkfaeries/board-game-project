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

    public async Task<(bool Ok, string? Error)> CreateAsync()
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
            return (false, "Код игры не может быть пустым.");
        }

        try
        {
            await EnsureConnectedAsync();

            var code = gameCode.Trim().ToUpperInvariant();
            var players = await _hub!.InvokeAsync<int>("JoinSession", code);

            GameCode = code;
            PlayerCount = players;
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
            _logger.LogWarning(ex, "Ошибка при отключении");
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
        _logger.LogError(ex, "Ошибка клиента");

        var message = ex switch
        {
            HubException => $"Ошибка хаба: {ex.Message}",
            HttpRequestException => $"Ошибка сети: {ex.Message}",
            _ => $"Непредвиденная ошибка: {ex.Message}",
        };

        return (false, message);
    }

    private void ClearSession()
    {
        GameCode = null;
        PlayerCount = 0;
    }
}
