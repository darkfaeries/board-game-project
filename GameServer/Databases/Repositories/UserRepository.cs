namespace GameServer;

public class UserRepository : IRepository<User>
{
    private readonly UserDbContext _context;

    public UserRepository(UserDbContext context)
    {
        _context = context;
    }

    public IEnumerable<User> GetAll()
    {
        return _context.Users.ToList();
    }

    public User? Get(string username)
    {
        return _context.Users.FirstOrDefault(p => p.Username == username);
    }

    public void Create(User item)
    {
        _context.Users.Add(item);
        Save();
    }

    public void Update(User item)
    {
        _context.Users.Update(item);
        Save();
    }

    public void Delete(string username)
    {
        var player = Get(username);
        if (player != null)
        {
            _context.Users.Remove(player);
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