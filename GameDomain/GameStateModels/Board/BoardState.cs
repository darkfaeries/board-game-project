#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class BoardState
{
    public Dictionary<Guid, Hex> Hexes { get; set; } = new Dictionary<Guid, Hex>();
    public Dictionary<Guid, Intersection> Intersections { get; set; } = new Dictionary<Guid, Intersection>();
    public Dictionary<Guid, Path> Paths { get; set; } = new Dictionary<Guid, Path>();
    public Dictionary<Guid, OccupantRef> OccupantsByIntersection { get; set; } = new Dictionary<Guid, OccupantRef>();
    public Dictionary<EnemyType, Enemy> Enemies { get; set; } = new Dictionary<EnemyType, Enemy>();

    public List<Path> GetAdjacentPaths(Guid intersectionId)
    {
        return Paths.Values
            .Where(path => path.FromIntersectionId == intersectionId
                || path.ToIntersectionId == intersectionId)
            .ToList();
    }

    public bool IsOccupied(Guid intersectionId)
        => OccupantsByIntersection.ContainsKey(intersectionId);
}
