namespace GameServer;

public interface IFriendNotifier
{
    Task RequestReceivedAsync(Guid toUserId, FriendRequestDto request);
    Task RequestAcceptedAsync(Guid toUserId, FriendDto newFriend);
    Task FriendRemovedAsync(Guid toUserId, Guid removedByUserId);
    Task StatusChangedAsync(Guid userId);
}
