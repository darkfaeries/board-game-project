using GameServer;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IGameStore, GameStore>();
builder.Services.AddTransient<ITurnService, TurnService>();
builder.Services.AddSignalR(options => options.MaximumReceiveMessageSize = 1024 * 1024);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration));

var app = builder.Build();

app.MapHub<GameHub>("/gamehub");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.Run();