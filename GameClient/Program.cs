using GameClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

const string windowedArg = "--windowed";
const string windowNumberArg = "--player-window=";
bool windowed = args.Contains(windowedArg, StringComparer.Ordinal);
var playerWindow = args.FirstOrDefault(arg => arg.StartsWith(windowNumberArg, StringComparison.Ordinal));
int? windowNumber = null;
if (playerWindow is not null && int.TryParse(playerWindow.AsSpan(windowNumberArg.Length), out int number) &&
    number is >= 1 and <= 4)
    windowNumber = number;

var hostArgs = args.Where(arg => arg != windowedArg &&
    !arg.StartsWith(windowNumberArg, StringComparison.Ordinal)).ToArray();
var builder = Host.CreateApplicationBuilder(hostArgs);
if (windowNumber.HasValue)
{
    // The development account is shared. Local multiplayer needs a separate guest per window.
    builder.Configuration["AutoLoginDev"] = "false";
}

builder.Services.AddSingleton(services => new GameClient.GameClient(
    "http://localhost:5285", services.GetRequiredService<ILogger<GameClient.GameClient>>()));
builder.Services.AddHostedService<DevAuthWorker>();

var host = builder.Build();
await using var hostLifetime = (IAsyncDisposable)host;

try
{
    // SDL expects the game window to be created on the main thread on macOS.
    host.Start();
    using var game = new GameClient.Client(
        host.Services.GetRequiredService<GameClient.GameClient>(), windowed, windowNumber);
    game.Run();
}
finally
{
    await host.StopAsync();
}
