#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class ResourceInventory
{
    public Dictionary<ResourceType, int> Resources { get; set; } = new(){
        { ResourceType.Bone, 1 },
        { ResourceType.Flint, 0 },
        { ResourceType.Hides, 1 },
        { ResourceType.Meat, 0 }
    };

    public int Get(ResourceType resourceType)
    {
        return Resources.GetValueOrDefault(resourceType);
    }

    public void Add(ResourceType resourceType, int amount)
    {
        if (Resources.ContainsKey(resourceType))
        {
            Resources[resourceType] += amount;
        }
        else
        {
            Resources[resourceType] = amount;
        }
    }

    public bool Spend(ResourceType resourceType, int amount)
    {
        if (!Resources.ContainsKey(resourceType) || Resources[resourceType] < amount)
        {
            return false;
        }

        Resources[resourceType] -= amount;
        return true;
    }

    public bool CanAfford(IReadOnlyDictionary<ResourceType, int> cost)
    {
        foreach (var (resource, requiredAmount) in cost)
        {
            if (Get(resource) < requiredAmount)
            {
                return false;
            }
        }

        return true;
    }

    public int CountTotal()
    {
        return Resources.Values.Sum();
    }
}
