using Microsoft.EntityFrameworkCore;

namespace GameServer;

public class UserDbContext(DbContextOptions<UserDbContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
}