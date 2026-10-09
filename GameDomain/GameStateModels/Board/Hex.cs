#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class Hex
{
    public AxialCoords Coords { get; }
    public TerrainType Terrain { get; }
    public int NumberToken { get; }
    public Region Region { get; }
    public bool IsDesertified { get; set;}

    public Hex(AxialCoords coords, TerrainType terrain, int number, Region region)
    {
        Coords = coords;
        Terrain = terrain;
        NumberToken = number;
        Region = region;
        IsDesertified = false;
    }

    public ResourceType GetProducedResource()
    {
        return (Resource)TerrainType;
    }
}
