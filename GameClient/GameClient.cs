using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Buffers;
using Microsoft.Extensions.Logging;

namespace GameClient;

public class GameClient
{
    private ClientWebSocket? _ws;
    private readonly string _baseUrl;
    private ILogger<GameClient> _logger;

    public string? GameCode { get; private set; }
    public bool IsConnected => _ws?.State == WebSocketState.Open;
    public event Action<int>? PlayerCountChanged;

    public GameClient(string baseUrl, ILogger<GameClient> logger)
    {
        _baseUrl = baseUrl;
        _logger = logger;
    }

    public async Task<(bool IsSuccess, string? Data)> CreateAsync()
    {
        var ws = new ClientWebSocket();

        try
        {
            await ws.ConnectAsync(
                new Uri($"ws://{_baseUrl}/ws/game/create"),
                CancellationToken.None);

            var buffer = new byte[1024 * 4];
            var result = await ws.ReceiveAsync(buffer, CancellationToken.None);

            var json = Encoding.UTF8.GetString(buffer, 0, result.Count);

            using var doc = JsonDocument.Parse(json);
            var code = doc.RootElement.GetProperty("Code").GetString();

            if (_ws is not null)
                await DisconnectAsync();

            _ws = ws;
            GameCode = code;

            return (true, code);
        }
        catch (WebSocketException ex)
        {
            ws.Dispose();
            _logger.LogCritical("Network/protocol error {message}", ex.Message);
            return (false, $"Ошибка сети/протокола: {ex.Message}");
        }
        catch (JsonException)
        {
            ws.Dispose();
            _logger.LogCritical("Error: Failed to parse JSON from the server");
            return (false, "Ошибка: Не удалось распарсить JSON от сервера.");
        }
        catch (Exception ex)
        {
            ws.Dispose();
            _logger.LogCritical("Unexpected error {message}", ex.Message);
            return (false, $"Непредвиденная ошибка: {ex.Message}");
        }
    }

    public async Task<(bool IsSuccess, string? Error)> ConnectAsync(string gameCode)
    {
        if (string.IsNullOrWhiteSpace(gameCode))
            return (false, "Код игры не может быть пустым.");

        if (_ws is not null)
            await DisconnectAsync();

        var ws = new ClientWebSocket();

        try
        {
            await ws.ConnectAsync(
                new Uri($"ws://{_baseUrl}/ws/game/connect?code={Uri.EscapeDataString(gameCode.Trim())}"),
                CancellationToken.None);

            _ws = ws;
            GameCode = gameCode.Trim().ToUpperInvariant();

            return (true, null);
        }
        catch (WebSocketException ex)
        {
            ws.Dispose();
            _logger.LogCritical("Network/protocol error {message}", ex.Message);
            return (false, $"Ошибка сети/протокола: {ex.Message}");
        }
        catch (Exception ex)
        {
            ws.Dispose();
            _logger.LogCritical("Unexpected error {message}", ex.Message);
            return (false, $"Непредвиденная ошибка: {ex.Message}");
        }
    }

    public async Task ListenAsync(CancellationToken cancellationToken = default)
    {
        var ws = _ws ?? throw new InvalidOperationException("WebSocket не подключён.");

        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        using var message = new MemoryStream();

        try
        {
            while (ws.State == WebSocketState.Open &&
                   !cancellationToken.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(buffer, cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.MessageType != WebSocketMessageType.Text)
                    continue;

                message.Write(buffer, 0, result.Count);

                if (!result.EndOfMessage)
                    continue;

                using var document = JsonDocument.Parse(message.GetBuffer().AsMemory(0, (int)message.Length));
                var root = document.RootElement;

                if (root.TryGetProperty("Type", out var type) &&
                    type.GetString() == "playerCount" &&
                    root.TryGetProperty("Count", out var count))
                {
                    PlayerCountChanged?.Invoke(count.GetInt32());
                }

                message.SetLength(0);
            }
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogCritical("Unexpected error {message}", ex.Message);
        }
        catch (WebSocketException ex)
        {
            _logger.LogCritical("Network/protocol error {message}", ex.Message);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public async Task DisconnectAsync()
    {
        if (_ws is null)
            return;

        try
        {
            if (_ws.State == WebSocketState.Open)
            {
                await _ws.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Client disconnected",
                    CancellationToken.None);
            }
        }
        catch (WebSocketException ex)
        {
            _logger.LogCritical("Network/protocol error {message}", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogCritical("Unexpected error {message}", ex.Message);
            throw;
        }
        finally
        {
            _ws.Dispose();
            _ws = null;
            GameCode = null;
        }
    }
}