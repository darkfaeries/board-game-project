using System;
using Microsoft.Extensions.Logging;
using GameServer;

using var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
var logger = loggerFactory.CreateLogger("Sandbox");

int seed = 42;
if (args.Length > 1 || (args.Length == 1 && !int.TryParse(args[0], out seed)))
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

using var game = new Sandbox.Game1(loggerFactory, seed, earthHeights);
game.Run();
return 0;
