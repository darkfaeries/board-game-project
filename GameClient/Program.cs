using GameClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(services =>
{
    var logger = services.GetRequiredService<ILogger<GameClient.GameClient>>();

    return new GameClient.GameClient(
        "http://localhost:5285",
        logger);
});

builder.Services.AddHostedService<DevAuthWorker>();

using var host = builder.Build();

await host.StartAsync();

// из коммита ильи
// using var game = new GameClient.Client();
// game.Run();

await host.StopAsync();