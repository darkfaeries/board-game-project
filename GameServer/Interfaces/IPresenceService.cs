namespace GameServer;

public interface IPresenceService
{
    public PresenceStatus GetStatus(Guid userId);

}