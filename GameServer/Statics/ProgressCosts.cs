namespace GameServer;

public static class ProgressCosts
{
    public static Dictionary<ResourceType, int> GetCost(ProgressType progressType, int targetLevel)
    {
        var cost = new Dictionary<ResourceType, int>();

        if (targetLevel < 1 || targetLevel > 5) 
            return cost; 

        ResourceType[] resourceOrder = progressType switch
        {
            ProgressType.Clothing => [ResourceType.Hides, ResourceType.Bone, ResourceType.Flint, ResourceType.Meat],
            ProgressType.Shelter  => [ResourceType.Bone, ResourceType.Flint, ResourceType.Hides, ResourceType.Meat],
            ProgressType.Food     => [ResourceType.Meat, ResourceType.Hides, ResourceType.Bone, ResourceType.Flint],
            ProgressType.Hunting  => [ResourceType.Flint, ResourceType.Meat, ResourceType.Hides, ResourceType.Bone],
            _ => throw new ArgumentOutOfRangeException(nameof(progressType))
        };

        int resourceCount = targetLevel switch
        {
            1 => 1,
            2 => 2,
            3 => 3,
            4 => 3,
            5 => 4,
            _ => 0
        };

        for (int i = 0; i < resourceCount; i++)
        {
            cost[resourceOrder[i]] = 1;
        }

        return cost;
    }
}