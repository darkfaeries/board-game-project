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

    public static Dictionary<ResourceType, int> AdvanceLevelCost(int targetLevel)
    {
        return targetLevel switch
        {
            1 => _ = new Dictionary<ResourceType, int>
            {
                { ResourceType.Flint, 1 }
            },
            2 => _ = new Dictionary<ResourceType, int>
            {
                { ResourceType.Bone, 1 }
            },
            3 => _ = new Dictionary<ResourceType, int>
            {
                { ResourceType.Bone, 1 },
                { ResourceType.Flint, 1 }
            },
            4 => _ = new Dictionary<ResourceType, int>
            {
                { ResourceType.Bone, 1 },
                { ResourceType.Flint, 1 }
            },
            5 => _ = new Dictionary<ResourceType, int>
            {
                { ResourceType.Hides, 1 },
                { ResourceType.Bone, 1 },
                { ResourceType.Flint, 1 }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(targetLevel), "Target level must be between 1 and 5.")
        };
    }
}
