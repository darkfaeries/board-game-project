#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class Hex
{
    public Guid Id { get; set; }
    public TerrainType Terrain { get; set; }
    public int NumberToken { get; set; }
    public Region Region { get; set; }
    public bool IsDesertified { get; set;}

    public ResourceType GetProducedResource()
    {
        throw new NotImplementedException();
    }
}
