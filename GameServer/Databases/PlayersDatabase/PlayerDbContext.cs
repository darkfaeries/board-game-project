using Microsoft.EntityFrameworkCore;

namespace GameServer;

public class PlayerDbContext(ILogger<PlayerDbContext> logger) : DbContext
{
    public DbSet<Player> Players { get; set; }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                logger.LogCritical("ConnectionStrings environment variable is missing");
                throw new ArgumentException("ConnectionStrings not set");
            }

            optionsBuilder.UseNpgsql(connectionString);

            logger.LogDebug($"Database for Players sucessfully configured");
    }
}