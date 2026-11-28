using GameServer;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IGameStore, GameStore>();
builder.Services.AddSingleton<IBoardFactory>(serviceProvider =>
{
    var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
    var mapsPath = System.IO.Path.Combine(
        environment.ContentRootPath, "GameFiles", "Maps");
    var logger = serviceProvider.GetRequiredService<ILogger<BoardFactory>>();
    return new BoardFactory(mapsPath, logger);
});
builder.Services.AddTransient<ITurnService, TurnService>();
builder.Services.AddTransient<IPlayerService, PlayerService>();
builder.Services.AddSignalR();

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHub<GameHub>("/gamehub");
app.Run();