using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;
using System.Threading.Channels;

// Run from the repository root after docker compose up -d --build.
string host = (Environment.GetEnvironmentVariable("GAME_SERVER_URL") ?? "http://localhost:5285").TrimEnd('/');
host = host.Replace("ws://", "http://").Replace("wss://", "https://");
if (!host.EndsWith("/gamehub")) host += "/gamehub";
string map = Environment.GetEnvironmentVariable("GAME_MAP_DIR") ?? "GameDomain/GameFiles/Maps/Default_Map";
using var hexes = new StreamReader(System.IO.Path.Combine(map, "hexes.csv"));
using var nodes = new StreamReader(System.IO.Path.Combine(map, "nodes.csv"));
using var paths = new StreamReader(System.IO.Path.Combine(map, "paths.csv"));
var local = new BoardState(hexes, nodes, paths);
var state = BoardSnapshot.FromBoardState(local);
state.Hexes[0].NumberToken = 12;
state.Hexes[0].IsDesertified = true;
state.ExplorationPaths[0].Counter = new CounterSnapshot { Kind = "adaptation", Id = Guid.NewGuid(), Color = AdaptationColor.Red };
// Verify Path identity survives reconstruction and reversed endpoints.
var edge = local.BlockedPaths.First();
Check(local.BlockedPaths.Contains(new Path(new AxialCoords(edge.to.q, edge.to.r), new AxialCoords(edge.from.q, edge.from.r))), "Undirected path identity");
var reconstructed = state.ToBoardState();
Check(JsonSerializer.Serialize(BoardSnapshot.FromBoardState(reconstructed)) == JsonSerializer.Serialize(state), "Snapshot domain roundtrip");
await using var creator = new HubConnectionBuilder().WithUrl(host).Build();
await using var joiner = new HubConnectionBuilder().WithUrl(host).Build();
var creatorCounts = Channel.CreateUnbounded<int>();
var joinerCounts = Channel.CreateUnbounded<int>();
creator.On<int>("PlayerCountChanged", n => creatorCounts.Writer.TryWrite(n));
joiner.On<int>("PlayerCountChanged", n => joinerCounts.Writer.TryWrite(n));
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await creator.StartAsync(timeout.Token);
await joiner.StartAsync(timeout.Token);
string suffix = Guid.NewGuid().ToString("N")[..6];
var created = await creator.InvokeAsync<GameConnectionResult>("CreateGame", state, "Creator-" + suffix, timeout.Token);
Check(created.Code.Length == 6, "Game code");
Check(await creatorCounts.Reader.ReadAsync(timeout.Token) == 1, "Creator is a player");
// ConnectGame takes only code and name: joiner sends no local board data.
var joined = await joiner.InvokeAsync<GameConnectionResult>("ConnectGame", created.Code.ToLowerInvariant(), "Joiner-" + suffix, timeout.Token);
Check(await creatorCounts.Reader.ReadAsync(timeout.Token) == 2, "Creator sees join");
Check(await joinerCounts.Reader.ReadAsync(timeout.Token) == 2, "Joiner sees count");
Check(JsonSerializer.Serialize(joined.State) == JsonSerializer.Serialize(state), "Server supplies complete modified board");
joined.State.ToBoardState();
bool rejected = false;
try { await joiner.InvokeAsync<GameConnectionResult>("ConnectGame", created.Code, "Again-" + suffix, timeout.Token); }
catch (Microsoft.AspNetCore.SignalR.HubException) { rejected = true; }
Check(rejected, "Repeated connection rejected");
Check(await joiner.InvokeAsync<int>("LeaveSession", created.Code, timeout.Token) == 1, "Leave count");
Check(await creatorCounts.Reader.ReadAsync(timeout.Token) == 1, "Creator sees leave");
await creator.InvokeAsync<int>("LeaveSession", created.Code, timeout.Token);
rejected = false;
try { await joiner.InvokeAsync<GameConnectionResult>("ConnectGame", created.Code, "Joiner-" + suffix, timeout.Token); }
catch (Microsoft.AspNetCore.SignalR.HubException) { rejected = true; }
Check(rejected, "Empty session deleted");
Console.WriteLine("PASS: snapshot roundtrip, blocked paths, create, join, server state, counts, repeated join, leave");

static void Check(bool value, string name)
{
    if (!value) throw new InvalidOperationException("FAIL: " + name);
    Console.WriteLine("OK: " + name);
}
