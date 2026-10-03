#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class ResourceInventory
{
    public int Meat { get; set; }
    public int Bone { get; set; }
    public int Flint { get; set; }
    public int Hides { get; set; }

    public int Get(ResourceType resourceType)
    {
        switch (resourceType)
        {
            case ResourceType.Meat: return Meat;
            case ResourceType.Bone: return Bone;
            case ResourceType.Flint: return Flint;
            case ResourceType.Hides: return Hides;
            default: throw new ArgumentOutOfRangeException(nameof(resourceType));
        }
    }

    public void Add(ResourceType resourceType, int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        switch (resourceType)
        {
            case ResourceType.Meat: Meat = checked(Meat + amount); break;
            case ResourceType.Bone: Bone = checked(Bone + amount); break;
            case ResourceType.Flint: Flint = checked(Flint + amount); break;
            case ResourceType.Hides: Hides = checked(Hides + amount); break;
            default: throw new ArgumentOutOfRangeException(nameof(resourceType));
        }
    }

    public void Spend(ResourceType resourceType, int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (Get(resourceType) < amount)
            throw new InvalidOperationException("Not enough resources.");

        switch (resourceType)
        {
            case ResourceType.Meat: Meat -= amount; break;
            case ResourceType.Bone: Bone -= amount; break;
            case ResourceType.Flint: Flint -= amount; break;
            case ResourceType.Hides: Hides -= amount; break;
            default: throw new ArgumentOutOfRangeException(nameof(resourceType));
        }
    }

    public int CountTotal() => checked(Meat + Bone + Flint + Hides);
}
