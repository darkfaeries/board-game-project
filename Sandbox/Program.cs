using System;
using Microsoft.Extensions.Logging;
using GameServer;

MapType mapType = MapType.Classic;

using var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
var logger = loggerFactory.CreateLogger("Sandbox");

if (args.Length > 0)
{
    if (!Enum.TryParse<MapType>(args[0], ignoreCase: true, out mapType)
        || !Enum.IsDefined(mapType))
    {
        logger.LogError("Unknown map type. Use Classic, Earth or BySeed.");
        return 1;
    }
}

int seed = 42;
if (args.Length > 2 || (args.Length == 2 && !int.TryParse(args[1], out seed)))
{
    logger.LogError("Usage: Sandbox [seed]. Seed must be a 32-bit integer.");
    return 1;
}

var pngPath = System.IO.Path.Combine(
    AppContext.BaseDirectory, "MapGeneration", "Templates", "Earth", "earth-height.png");
var pngReader = new PngReader(loggerFactory.CreateLogger<PngReader>(), pngPath);
var loggerArrayHeightSourse =  loggerFactory.CreateLogger<ArrayHeightSource>();
IHeightSource earthHeights = new ArrayHeightSource(loggerArrayHeightSourse, pngReader.Read());
logger.LogInformation("Loaded Earth heights from {PngPath}.", pngPath);

using var game = new Sandbox.Game1(loggerFactory, seed, earthHeights, mapType);
game.Run();
return 0;
