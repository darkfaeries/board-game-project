namespace GameServer;

public class FriendService : IFriendService
{
    private readonly IFriendshipStore _store;
    private readonly ILogger<FriendService> _logger;

    public FriendService(IFriendshipStore store, ILogger<FriendService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<List<UserSearchResultDto>> SearchAsync(Guid userId, string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            throw new FriendException("Enter at least 2 characters.");
        }

        var users = await _store.SearchUsersAsync(query, userId, 20);
        var result = new List<UserSearchResultDto>();
        foreach (var user in users)
        {
            var link = await _store.FindBetweenAsync(userId, user.Id);
            result.Add(new UserSearchResultDto(user.Id, user.Username, GetRelation(userId, link)));
        }
        return result;
    }

    public async Task SendRequestAsync(Guid userId, Guid targetId)
    {
        if (userId == targetId)
        {
            throw new FriendException("You cannot add yourself.");
        }

        var existing = await _store.FindBetweenAsync(userId, targetId);
        if (existing != null)
        {
            if (existing.Status == FriendshipStatus.Accepted)
            {
                throw new FriendException("You are already friends.");
            }
            if (existing.RequesterId == userId)
            {
                throw new FriendException("Request already sent.");
            }
            await AcceptRequestAsync(userId, existing.Id);
            return;
        }

        var friendship = new Friendship
        {
            Id = Guid.NewGuid(),
            RequesterId = userId,
            AddresseeId = targetId,
            Status = FriendshipStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        await _store.AddAsync(friendship);
        _logger.LogInformation($"Friend request {friendship.Id}: {userId} -> {targetId}");
    }

    public async Task AcceptRequestAsync(Guid userId, Guid requestId)
    {
        var request = await _store.GetAsync(requestId);
        if (request == null || request.AddresseeId != userId || request.Status != FriendshipStatus.Pending)
        {
            throw new FriendException("Request not found.");
        }

        request.Status = FriendshipStatus.Accepted;
        request.RespondedAt = DateTime.UtcNow;
        await _store.UpdateAsync(request);
        _logger.LogInformation($"Friend request {requestId} accepted by {userId}");
    }

    public async Task DeclineRequestAsync(Guid userId, Guid requestId)
    {
        var request = await _store.GetAsync(requestId);
        if (request == null || request.AddresseeId != userId || request.Status != FriendshipStatus.Pending)
        {
            throw new FriendException("Request not found.");
        }

        await _store.DeleteAsync(request);
        _logger.LogInformation($"Friend request {requestId} declined by {userId}");
    }

    public async Task RemoveFriendAsync(Guid userId, Guid friendId)
    {
        var link = await _store.FindBetweenAsync(userId, friendId);
        if (link == null || link.Status != FriendshipStatus.Accepted)
        {
            throw new FriendException("You are not friends with this user.");
        }

        await _store.DeleteAsync(link);
    }

    public async Task<List<FriendDto>> GetFriendsAsync(Guid userId)
    {
        var links = await _store.GetAcceptedAsync(userId);
        var friendIds = links.Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId).ToList();
        var names = await _store.GetUsernamesAsync(friendIds);

        return friendIds.Where(names.ContainsKey).Select(id => new FriendDto(id, names[id], PresenceStatus.Offline))
            .OrderByDescending(f => f.Status != PresenceStatus.Offline).ThenBy(f => f.Username).ToList();
    }

    public async Task<List<FriendRequestDto>> GetRequestsAsync(Guid userId)
    {
        var incoming = await _store.GetIncomingAsync(userId);
        var names = await _store.GetUsernamesAsync(incoming.Select(r => r.RequesterId));

        return incoming.Where(r => names.ContainsKey(r.RequesterId)).OrderByDescending(r => r.CreatedAt)
            .Select(r => new FriendRequestDto(r.Id, r.RequesterId, names[r.RequesterId], r.CreatedAt)).ToList();
    }
    

    private static RelationState GetRelation(Guid me, Friendship? link)
    {
        if (link == null)
        {
            return RelationState.None;
        }
        else if (link.Status == FriendshipStatus.Accepted)
        {
            return RelationState.Friends;
        }
        else if (link.RequesterId == me)
        {
            return RelationState.RequestSent;
        }
        else
        {
            return RelationState.RequestReceived;
        }
    }
}