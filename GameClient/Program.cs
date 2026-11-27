bool windowed = args.Contains("--windowed", StringComparer.Ordinal);
int? windowNumber = null;
const string windowArg = "--player-window=";
var playerWindow = args.FirstOrDefault(arg => arg.StartsWith(windowArg, StringComparison.Ordinal));
if (playerWindow is not null && int.TryParse(playerWindow.AsSpan(windowArg.Length), out int number) &&
    number is >= 1 and <= 4)
    windowNumber = number;

using var game = new GameClient.Client(windowed, windowNumber);
game.Run();
