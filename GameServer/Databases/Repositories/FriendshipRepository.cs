using Microsoft.EntityFrameworkCore;

namespace GameServer;

public class EfFriendshipStore : IFriendshipStore
{
    private readonly UserDbContext _db;

    public EfFriendshipStore(UserDbContext db) => _db = db;

    public async Task<List<UserInfo>> SearchUsersAsync(string query, Guid exceptUserId, int limit)
    {
        var q = query.Trim().ToLower();
        return await _db.Users
            .Where(u => u.Id != exceptUserId && u.Username.ToLower().Contains(q))
            .OrderBy(u => u.Username).Take(limit).Select(u => new UserInfo(u.Id, u.Username)).ToListAsync();
    }

    public async Task<Dictionary<Guid, string>> GetUsernamesAsync(IEnumerable<Guid> ids)
    {
        var list = ids.Distinct().ToList();
        return await _db.Users.Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);
    }

    public Task<Friendship?> FindBetweenAsync(Guid a, Guid b) =>
        _db.Friendships.FirstOrDefaultAsync(f =>
            (f.RequesterId == a && f.AddresseeId == b) ||
            (f.RequesterId == b && f.AddresseeId == a));

    public Task<Friendship?> GetAsync(Guid id) =>
        _db.Friendships.FirstOrDefaultAsync(f => f.Id == id);

    public Task<List<Friendship>> GetAcceptedAsync(Guid userId) =>
        _db.Friendships.Where(f => f.Status == FriendshipStatus.Accepted && (f.RequesterId == userId || f.AddresseeId == userId)).ToListAsync();

    public Task<List<Friendship>> GetIncomingAsync(Guid userId) =>
        _db.Friendships.Where(f => f.Status == FriendshipStatus.Pending && f.AddresseeId == userId).ToListAsync();


    
    
    public async Task AddAsync(Friendship friendship)
    {
        _db.Friendships.Add(friendship);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Friendship friendship)
    {
        _db.Friendships.Update(friendship);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Friendship friendship)
    {
        _db.Friendships.Remove(friendship);
        await _db.SaveChangesAsync();
    }
}