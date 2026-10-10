namespace GameServer;

public static class GameCosts
{
    public static readonly Dictionary<ResourceType, int> CampCost = new()
    {
        { ResourceType.Hides, 1 },
        { ResourceType.Bone, 1 },
        { ResourceType.Flint, 1 } 
    };

    public static readonly Dictionary<ResourceType, int> ExplorerCost = new()
    {
        { ResourceType.Meat, 1 },
        { ResourceType.Hides, 1 }
    };

    public static readonly Dictionary<ResourceType, int> MoveExplorerCost = new()
    {
        { ResourceType.Meat, 1 }
    };
}