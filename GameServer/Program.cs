using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using GameServer;
using WebAPI.API;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Npgsql;

var envPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Directory.GetCurrentDirectory(), "../.env"));
if (File.Exists(envPath))
    DotNetEnv.Env.Load(envPath);

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        $"Database connection is missing. Add ConnectionStrings__DefaultConnection to {envPath} " +
        "using the format in the repository's .env.example file.");
}

var connectionSettings = new NpgsqlConnectionStringBuilder(connectionString);
if (string.IsNullOrWhiteSpace(connectionSettings.Host))
{
    throw new InvalidOperationException(
        $"Database connection in {envPath} has no Host. Set Host=localhost (or your PostgreSQL host).");
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "JWT token",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("bearer", document),
            new List<string>()
        }
    });
});


builder.Services.AddSingleton<IGameStore, GameStore>();
builder.Services.AddSingleton<ConnectionManager>();
builder.Services.AddSingleton<IBoardFactory>(serviceProvider =>
{
    var mapsPath = System.IO.Path.Combine(
        AppContext.BaseDirectory, "GameFiles", "Maps");
    var logger = serviceProvider.GetRequiredService<ILogger<BoardFactory>>();
    return new BoardFactory(mapsPath, logger);
});
builder.Services.AddTransient<IBoardService>(serviceProvider =>
{
    var mapsPath = System.IO.Path.Combine(AppContext.BaseDirectory, "GameFiles", "Maps");
    var logger = serviceProvider.GetRequiredService<ILogger<BoardService>>();
    return new BoardService(mapsPath, logger);
});
builder.Services.AddTransient<ITurnService, TurnService>();
builder.Services.AddTransient<IPlayerService, PlayerService>();
builder.Services.AddTransient<IGameService, GameService>();
builder.Services.AddTransient<IRepository<User>, UserRepository>();
builder.Services.AddSignalR();

builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration));

var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["Key"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey!))
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/gamehub"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHub<GameHub>("/gamehub");

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<UserDbContext>();
    dbContext.Database.Migrate(); 
}

app.Run();
