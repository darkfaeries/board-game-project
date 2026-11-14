namespace GameServer;

public enum FriendshipStatus { Pending = 0, Accepted = 1 }

public class Friendship
{
    public Guid Id { get; set; }
    public Guid RequesterId { get; set; }
    public Guid AddresseeId { get; set; }
    public FriendshipStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}

public sealed record UserInfo(Guid Id, string Username);

public sealed class FriendException : Exception
{
    public FriendException(string message) : base(message) { }
}
