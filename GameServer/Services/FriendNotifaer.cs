using Microsoft.AspNetCore.SignalR;
using WebAPI.API;

namespace GameServer;

public static class FriendGroups
{
    public static string For(Guid userId) => $"user-{userId}";
}


internal sealed class FriendNotifier : IFriendNotifier
{
    private readonly IHubContext<GameHub> _hub;
    private readonly IFriendshipStore _store;

    public FriendNotifier(IHubContext<GameHub> hub, IFriendshipStore store)
    {
        _hub = hub;
        _store = store;
    }

    public Task RequestReceivedAsync(Guid toUserId, FriendRequestDto request) =>
        _hub.Clients.Group(FriendGroups.For(toUserId)).SendAsync("FriendRequestReceived", request);

    public Task RequestAcceptedAsync(Guid toUserId, FriendDto newFriend) =>
        _hub.Clients.Group(FriendGroups.For(toUserId)).SendAsync("FriendRequestAccepted", newFriend);

    public Task FriendRemovedAsync(Guid toUserId, Guid removedByUserId) =>
        _hub.Clients.Group(FriendGroups.For(toUserId)).SendAsync("FriendRemoved", removedByUserId);

    public async Task StatusChangedAsync(Guid userId)
    {
        var links = await _store.GetAcceptedAsync(userId);
        var groups = links
            .Select(f => FriendGroups.For(f.RequesterId == userId ? f.AddresseeId : f.RequesterId))
            .ToList();

        if (groups.Count == 0)
        {
            return;
        }

        var dto = new FriendStatusChangedDto(userId);
        await _hub.Clients.Groups(groups).SendAsync("FriendStatusChanged", dto);
    }
}