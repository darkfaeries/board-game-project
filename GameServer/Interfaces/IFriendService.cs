namespace GameServer;

public interface IFriendService
{
    Task<List<UserSearchResultDto>> SearchAsync(Guid userId, string query);
    Task SendRequestAsync(Guid userId, Guid targetId);
    Task AcceptRequestAsync(Guid userId, Guid requestId);
    Task DeclineRequestAsync(Guid userId, Guid requestId);
    Task RemoveFriendAsync(Guid userId, Guid friendId);
    Task<List<FriendDto>> GetFriendsAsync(Guid userId);
    Task<List<FriendRequestDto>> GetRequestsAsync(Guid userId);
}