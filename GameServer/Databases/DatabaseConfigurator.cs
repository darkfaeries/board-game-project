using Microsoft.EntityFrameworkCore;

namespace GameServer;

public static class DatabaseConfigurator
{
    public static void Configure(DbContextOptionsBuilder options, IConfiguration configuration)
    {
        var dbProvider = configuration["DatabaseProvider"] ?? "PostgreSQL";

        if (dbProvider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            options.UseInMemoryDatabase("GameDevMemoryDb");
        }
        else
        {
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
        }
    }
    public static void ApplyMigrationsIfPostgres<TContext>(IHost app) where TContext : DbContext
    {
        var configuration = app.Services.GetRequiredService<IConfiguration>();
        var dbProvider = configuration["DatabaseProvider"] ?? "PostgreSQL";

        if (dbProvider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        dbContext.Database.Migrate();
    }
}