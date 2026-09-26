using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<GameStore>();
// builder.Services.AddHttpLogging(o => { });

var app = builder.Build();

// app.UseHttpLogging();
app.UseWebSockets();

// create request from client, opens a socket connection and returns the code
app.Map("/ws/game/create", async (HttpContext context, GameStore store) =>
{
    if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }

    var session = new GameSession();
    store.Sessions.TryAdd(session.Code, session);

    var webSocket = await context.WebSockets.AcceptWebSocketAsync();

    // send the code via socket 
    var codeMessage = new GameCodeMessage { Code = session.Code };
    var json = JsonSerializer.Serialize(codeMessage);
    var bytes = Encoding.UTF8.GetBytes(json);
    await webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);

    // add to future broadcasts
    await HandlePlayer(webSocket, session, store);
});

// connect to game via websocket; user added into player pool, game updates will be broadcast
app.Map("/ws/game/connect", async (HttpContext context, GameStore store) =>
{
    if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }

    // game code is sent in the query string; parse it and check in the store
    var gameCode = context.Request.Query["code"].ToString().ToUpper();
    if (string.IsNullOrEmpty(gameCode) || !store.Sessions.TryGetValue(gameCode, out var session))
    {
        context.Response.StatusCode = 404; // game not found
        return;
    }

    // player enters; create socket for them and run the listening loop
    var webSocket = await context.WebSockets.AcceptWebSocketAsync();

    await HandlePlayer(webSocket, session, store);
});

async Task HandlePlayer(WebSocket webSocket, GameSession session, GameStore store)
{
    // add player to list
    var playerId = Guid.NewGuid();
    session.Players.TryAdd(playerId, webSocket);

    // broadcast player count to everyone after this player's entry
    await BroadcastPlayerCount(session);

    // listen for messages
    var buffer = new byte[1024 * 4];
    try
    {
        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }
        }
    }
    finally
    {
        // delete from store on leave
        session.Players.TryRemove(playerId, out _);
        await BroadcastPlayerCount(session);
    }
}

// broadcast the new playercount to everyone active
async Task BroadcastPlayerCount(GameSession session)
{
    var count = session.Players.Count;

    var countMessage = new PlayerCountMessage { Count = count };
    var json = JsonSerializer.Serialize(countMessage);
    var bytes = Encoding.UTF8.GetBytes(json);

    foreach (var playerSocket in session.Players.Values)
    {
        if (playerSocket.State == WebSocketState.Open)
        {
            await playerSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}

app.Run();
