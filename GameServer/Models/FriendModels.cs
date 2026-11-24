public enum RelationState { None = 0, Friends = 1, RequestSent = 2, RequestReceived = 3 }
public enum PresenceStatus { Offline = 0, Online = 1, InLobby = 2, InGame = 3 }

public sealed record UserSearchResultDto(Guid UserId, string Username, RelationState Relation);
public sealed record FriendDto(Guid UserId, string Username, PresenceStatus Status);
public sealed record FriendRequestDto(Guid RequestId, Guid FromUserId, string FromUsername, DateTime CreatedAt);