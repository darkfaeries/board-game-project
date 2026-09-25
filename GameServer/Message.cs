// every message should have a Type field for client to parse correctly
// every type will have a corresponding handler
public class Message
{
    public string Type { get; set; }
}

// broadcast game code
public class GameCodeMessage
{
    public string Type { get; set; } = "gameCode";
    public string Code { get; set; }
}

// broadcast player count
public class PlayerCountMessage : Message
{
    public string Type { get; set; } = "playerCount";
    public int Count { get; set; }
}

// broadcast game info (not used yet)
public class GameInfoMessage
{
    public string Type { get; set; } = "gameInfo";
}