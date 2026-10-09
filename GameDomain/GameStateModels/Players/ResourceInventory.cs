#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class ResourceInventory
{
    public Dictionary<ResourceType, int> Resources { get; set; }

    public int Get(ResourceType resourceType)
    {
        return Resources[resourceType];
    }

    public void Add(ResourceType resourceType, int amount)
    {
        // TODO write
    }

    public void Spend(ResourceType resourceType, int amount)
    {
        // TODO write
    }

    public int CountTotal() 
    {
        // TODO write
    }
}
