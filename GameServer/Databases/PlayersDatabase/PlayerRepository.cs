namespace GameServer;

public class PlayerRepository : IRepository<Player>
{
    private readonly PlayerDbContext _context;

    public PlayerRepository(PlayerDbContext context)
    {
        _context = context;
    }

    public IEnumerable<Player> GetAll()
    {
        return _context.Players.ToList();
    }

    public Player? Get(string username)
    {
        return _context.Players.FirstOrDefault(p => p.Name == username);
    }

    public void Create(Player item)
    {
        _context.Players.Add(item);
        Save();
    }

    public void Update(Player item)
    {
        _context.Players.Update(item);
        Save();
    }

    public void Delete(string username)
    {
        var player = Get(username);
        if (player != null)
        {
            _context.Players.Remove(player);
            Save();
        }
    }

    public void Save()
    {
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}