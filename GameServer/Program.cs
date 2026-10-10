using GameServer;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration));

var app = builder.Build();

app.MapHub<GameHub>("/gamehub");
app.Run();