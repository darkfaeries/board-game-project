using GameServer;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IGameStore, GameStore>();
builder.Services.AddSingleton<ITurnService, TurnService>();
builder.Services.AddSingleton<IPlayerService, PlayerService>();
builder.Services.AddTransient<GameSession>();
builder.Services.AddSignalR();

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration));

var app = builder.Build();

app.MapHub<GameHub>("/gamehub");
app.Run();