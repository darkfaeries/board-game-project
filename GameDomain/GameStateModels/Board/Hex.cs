#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class Hex
{
    public Guid Id { get; }
    public TerrainType Terrain { get; }
    public int NumberToken { get; }
    public Region Region { get; }
    public bool IsDesertified { get; set;}

    public ResourceType GetProducedResource()
    {
        return (ResourceType)Terrain;
    }
}
