namespace GameServer;

public interface IFriendshipStore
{
    Task<List<UserInfo>> SearchUsersAsync(string query, Guid exceptUserId, int limit);
    Task<Dictionary<Guid, string>> GetUsernamesAsync(IEnumerable<Guid> ids);
    Task<Friendship?> FindBetweenAsync(Guid a, Guid b);
    Task<Friendship?> GetAsync(Guid id);
    Task<List<Friendship>> GetAcceptedAsync(Guid userId);
    Task<List<Friendship>> GetIncomingAsync(Guid userId);
    Task AddAsync(Friendship friendship);
    Task UpdateAsync(Friendship friendship);
    Task DeleteAsync(Friendship friendship);
}
