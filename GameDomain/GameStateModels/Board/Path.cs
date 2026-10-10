#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class Path
{
    public AxialCoords from { get; }
    public AxialCoords to { get; }

    public override int GetHashCode()
    {
        return from.GetHashCode() ^ to.GetHashCode();
    }

    public override bool Equals(object obj)
    {
        return obj is Path other && ((from.Equals(other.from) && to.Equals(other.to)) || (from.Equals(other.to) && to.Equals(other.from))); 
    }

    public Path(AxialCoords from_in, AxialCoords to_in)
    {
        if (from_in.IsHex()) throw new Exception("hex coords used for path constructor");
        if (to_in.IsHex()) throw new Exception("hex coords used for path constructor");
        from = from_in;
        to = to_in;
    }
}

public class ExplorationPath
{
    public int ClothingRequirement { get; }
    public int ShelterRequirement { get; }
    public ExplorationCounter? ExplorationCounter { get; set; }

    public ExplorationPath(int cloth_req, int shelter_req, Tribe tribe)
    {
        ClothingRequirement = cloth_req;
        ShelterRequirement = shelter_req;
        ExplorationCounter = null;
        // TODO add exploration counters; probably will need to be a global procedure though
    }
}
