using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace GameClient;

public class GameClient
{
    private ClientWebSocket? _ws;
    private readonly string _baseUrl;

    public string? GameCode { get; private set; }
    public bool IsConnected => _ws?.State == WebSocketState.Open;

    public GameClient(string baseUrl) => _baseUrl = baseUrl;

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

            _ws = ws;
            GameCode = code;

            ws.Dispose();

            return (true, code);
        }
        catch (WebSocketException ex)
        {
            ws.Dispose();
            return (false, $"Ошибка сети/протокола: {ex.Message}");
        }
        catch (JsonException)
        {
            ws.Dispose();
            return (false, "Ошибка: Не удалось распарсить JSON от сервера.");
        }
        catch (Exception ex)
        {
            ws.Dispose();
            return (false, $"Непредвиденная ошибка: {ex.Message}");
        }
    }
}