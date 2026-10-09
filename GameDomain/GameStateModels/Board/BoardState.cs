#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class BoardState
{
    public Dictionary<Guid, Hex> Hexes { get; set; } = new Dictionary<Guid, Hex>();
    public Dictionary<Guid, Node> Intersections { get; set; } = new Dictionary<Guid, Node>();
    public Dictionary<Guid, Path> Paths { get; set; } = new Dictionary<Guid, Path>();
    
    // won't be used to not double the info
    // public Dictionary<Guid, OccupantRef> OccupantsByIntersection { get; set; } = new Dictionary<Guid, OccupantRef>();

    // int is the hex id of neanderthal/tiger; we're getting rid of the Enemy class
    public int NeanderthalPosition { get; set; } = -1;
    public int SabertoothTigerPosition { get; set; } = -1;

    // should be an arithmetic function
    public List<Path> GetAdjacentPaths(Guid intersectionId)
    {
        // TODO today
        throw new NotImplementedException();
    }

    public bool IsOccupied(Guid nodeId)
    {
        throw new NotImplementedException();
    }
