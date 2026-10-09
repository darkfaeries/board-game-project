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

    public AxialCoords? NeanderthalPosition { get; set; } = null;
    public AxialCoords? SabertoothTigerPosition { get; set; } = null;

    // read board from csv file readers
    public BoardState(StreamReader hex_rd, StreamReader node_rd, StreamReader path_rd)
    {
        // TODO
    }

    // should be an arithmetic function
    public List<Path> GetAdjacentPaths(AxialCoords nodeCoords)
    {
        // TODO today
        throw new NotImplementedException();
    }

    public bool IsOccupied(Guid nodeId)
    {
        throw new NotImplementedException();
    }
}