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
    // Dict<position, userId>
    public Dictionary<AxialCoords, Guid> CampPositions { get; set; } = new Dictionary<AxialCoords, Guid>();
    public Dictionary<AxialCoords, Guid> ExplorerPositions { get; set; } = new Dictionary<AxialCoords, Guid>();

    public AxialCoords? NeanderthalPosition { get; set; } = null;
    public AxialCoords? SabertoothTigerPosition { get; set; } = null;

    public BoardState(
        Dictionary<AxialCoords, Hex> hexes, Dictionary<AxialCoords, Node> nodes, 
        Dictionary<Path, ExplorationPath> explorationPaths, HashSet<Path> blockedPaths)
    {
        Hexes = hexes;
        Nodes = nodes;
        ExplorationPaths = explorationPaths;
        BlockedPaths = blockedPaths;
    }

    public bool IsOccupied(AxialCoords coords) =>
        CampPositions.ContainsKey(coords) || ExplorerPositions.ContainsKey(coords);

}