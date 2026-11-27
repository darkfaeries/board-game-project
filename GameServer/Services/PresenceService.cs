using GameServer;

namespace GameServer;

public class PresenceService : IPresenceService
{
    private readonly ConnectionManager _connections;
    private readonly IGameStore _gameStore;

    public PresenceService(ConnectionManager connections, IGameStore gameStore)
    {
        _connections = connections;
        _gameStore = gameStore;
    }

    public PresenceStatus GetStatus(Guid userId)
    {
        if (!_connections.IsOnline(userId)) 
            return PresenceStatus.Offline;

        var sessions = _gameStore.GetSessions(); // Возвращает Dictionary<string, GameSession>
        
        foreach (var session in sessions.Values)
        {
            var player = session.GetGameState().GetPlayer(userId);
            
            if (player != null)
            {
                var status = session.GetGameState().Status;
                if (status == GameStatus.Finished)
                {
                    continue; 
                }
                if (status == GameStatus.WaitingForPlayers)
                {
                    return PresenceStatus.InLobby; 
                }
                return PresenceStatus.InGame; 
            }
        }
        return PresenceStatus.Online;
    }
}