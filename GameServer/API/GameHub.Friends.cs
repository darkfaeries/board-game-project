using Microsoft.AspNetCore.SignalR;
using GameServer;

namespace WebAPI.API;

partial class GameHub
{
    public Task<List<UserSearchResultDto>> SearchUsers(string query) =>
        FriendCall(userId => _friendService.SearchAsync(userId, query));

    public Task SendFriendRequest(Guid targetUserId) =>
        FriendAction(userId => _friendService.SendRequestAsync(userId, targetUserId));

    public Task AcceptFriendRequest(Guid requestId) =>
        FriendAction(userId => _friendService.AcceptRequestAsync(userId, requestId));

    public Task DeclineFriendRequest(Guid requestId) =>
        FriendAction(userId => _friendService.DeclineRequestAsync(userId, requestId));

    public Task RemoveFriend(Guid friendUserId) =>
        FriendAction(userId => _friendService.RemoveFriendAsync(userId, friendUserId));

    public Task<List<FriendDto>> GetFriends() =>
        FriendCall(userId => _friendService.GetFriendsAsync(userId));

    public Task<List<FriendRequestDto>> GetFriendRequests() =>
        FriendCall(userId => _friendService.GetRequestsAsync(userId));
    
    private async Task<T> FriendCall<T>(Func<Guid, Task<T>> action)
    {
        var userId = GetUserIdFromContext();
        try
        {
            return await action(userId);
        }
        catch (FriendException ex)
        {
            throw new HubException(ex.Message);
        }
    }

    private async Task FriendAction(Func<Guid, Task> action)
    {
        var userId = GetUserIdFromContext();
        try
        {
            await action(userId);
        }
        catch (FriendException ex)
        {
            throw new HubException(ex.Message);
        }
    }
}