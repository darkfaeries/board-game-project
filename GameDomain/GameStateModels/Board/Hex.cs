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
    public bool IsDesertified { get; set; }
    public List<Guid> IntersectionIds { get; set; } = new List<Guid>();

    public ResourceType GetProducedResource()
    {
        // The diagram does not specify the terrain-to-resource mapping
        // or the behavior of a desertified hex.
        throw new NotImplementedException();
    }
}
