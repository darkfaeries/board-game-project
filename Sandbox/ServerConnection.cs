using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace Sandbox;

// Background callbacks only queue data; Game1.Update owns rendering and screen transitions.
public sealed class ServerConnection : IDisposable
{
    public ConcurrentQueue<string> Messages { get; } = new();
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    private HubConnection _connection;
    private string _gameCode;
    private readonly string _username = Environment.GetEnvironmentVariable("GAME_PLAYER_NAME")
        ?? "Player-" + Guid.NewGuid().ToString("N")[..6];

    private async Task OpenAsync()
    {
        await DisconnectAsync();
        string url = (Environment.GetEnvironmentVariable("GAME_SERVER_URL") ?? "http://localhost:5285").TrimEnd('/');
        var uri = new UriBuilder(url);
        if (uri.Scheme == "ws") uri.Scheme = "http";
        if (uri.Scheme == "wss") uri.Scheme = "https";
        if (!uri.Path.TrimEnd('/').EndsWith("/gamehub", StringComparison.OrdinalIgnoreCase))
            uri.Path = uri.Path.TrimEnd('/') + "/gamehub";
        _connection = new HubConnectionBuilder().WithUrl(uri.Uri).Build();
        _connection.On<int>("PlayerCountChanged", count =>
            Messages.Enqueue(JsonSerializer.Serialize(new { Type = "playerCount", Count = count })));
        _connection.On<JsonElement>("TurnChanged", change =>
            Messages.Enqueue("Turn changed: " + change.ToString()));
        _connection.Closed += error =>
        {
            Messages.Enqueue(error == null ? "Disconnected" : "Connection error: " + error.Message);
            if (error != null) Messages.Enqueue("Disconnected");
            return Task.CompletedTask;
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await _connection.StartAsync(timeout.Token);
    }

    public async Task CreateGameAsync()
    {
        try
        {
            await OpenAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var result = await _connection.InvokeAsync<GameConnectionResult>("CreateSession", _username, timeout.Token);
            Accept(result);

        }
        catch { await DisconnectAsync(); throw; }
    }

    public async Task JoinGameAsync(string code)
    {
        try
        {
            await OpenAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var result = await _connection.InvokeAsync<GameConnectionResult>("JoinSession", code.Trim().ToUpperInvariant(), _username, timeout.Token);
            Accept(result);
        }
        catch { await DisconnectAsync(); throw; }
    }

    private void Accept(GameConnectionResult result)
    {
        if (result?.State == null || string.IsNullOrEmpty(result.Code)) throw new InvalidDataException("Server returned no state");
        _gameCode = result.Code;
        Messages.Enqueue(JsonSerializer.Serialize(new { Type = "gameState", result.Code, result.State }));
    }

    public async Task DisconnectAsync()
    {
        var connection = _connection;
        if (connection == null) return;
        try
        {
            if (connection.State == HubConnectionState.Connected && _gameCode != null)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await connection.InvokeAsync<int>("LeaveSession", _gameCode, timeout.Token);
            }
        }
        catch (Exception ex) { Messages.Enqueue("Leave: " + ex.Message); }
        finally
        {
            await connection.DisposeAsync();
            _connection = null; _gameCode = null;
        }
    }

    public void Dispose()
    {
        var connection = _connection;
        // Disposing the hub connection triggers the server's OnDisconnectedAsync.
        if (connection != null) _ = DisposeQuietly(connection);
    }
    private static async Task DisposeQuietly(HubConnection connection)
    {
        try { await connection.DisposeAsync(); } catch { }
    }
}
