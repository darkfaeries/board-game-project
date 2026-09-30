using GameServer;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IGameStore, GameStore>();
builder.Services.AddTransient<GameServer.ISession, GameSession>();
builder.Services.AddTransient<ITurnService, TurnService>();
builder.Services.AddTransient<GameSession>();
builder.Services.AddSingleton<Func<GameServer.ISession>>(sp => () => ActivatorUtilities.CreateInstance<GameSession>(sp));
builder.Services.AddSignalR();

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration));

var app = builder.Build();

app.MapHub<GameHub>("/gamehub");
app.Run();