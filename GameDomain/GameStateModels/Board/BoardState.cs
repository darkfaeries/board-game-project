#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class BoardState
{
    public Dictionary<AxialCoords, Hex> Hexes { get; } = new Dictionary<AxialCoords, Hex>();
    public Dictionary<AxialCoords, Node> Nodes { get; } = new Dictionary<AxialCoords, Node>();
    public Dictionary<Path, ExplorationPath> ExplorationPaths { get; } = new Dictionary<Path, ExplorationPath>();
    public HashSet<Path> BlockedPaths { get; } = new HashSet<Path>();
    public IReadOnlySet<Path> LandPaths { get; }
    public IReadOnlySet<Path> SeaPaths { get; }

    public AxialCoords? NeanderthalPosition { get; set; } = null;
    public AxialCoords? SabertoothTigerPosition { get; set; } = null;

    public BoardState(
        Dictionary<AxialCoords, Hex> hexes, Dictionary<AxialCoords, Node> nodes, 
        Dictionary<Path, ExplorationPath> explorationPaths, HashSet<Path> blockedPaths,
        IReadOnlySet<Path>? landPaths = null, IReadOnlySet<Path>? seaPaths = null)
    {
        Hexes = hexes;
        Nodes = nodes;
        ExplorationPaths = explorationPaths;
        BlockedPaths = blockedPaths;
        LandPaths = landPaths ?? new HashSet<Path>();
        SeaPaths = seaPaths ?? new HashSet<Path>();
    }

    public List<Path> GetAdjacentPaths(AxialCoords nodeCoords)
    {
        // TODO today
        throw new NotImplementedException();
    }

    public List<Path> GetAdjacentIntersections(AxialCoords nodeCoords)
    {
        // TODO today
        throw new NotImplementedException();
    }

    public bool IsOccupied(AxialCoords coords)
    {
        throw new NotImplementedException();
    }
}
