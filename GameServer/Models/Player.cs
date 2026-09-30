namespace GameServer;

public class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
}