namespace GameServer;

public class LandPathGenerator
{
    public IReadOnlyCollection<Path> Generate(IReadOnlyList<AxialCoords> continent, int extraPathCount, Random random, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(continent);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfNegative(extraPathCount);
        if (continent.Count == 0) 
            return Array.Empty<Path>();

        var hexEdges = CollectHexEdges(continent);
        var candidateEdges = hexEdges.Values.SelectMany(edges => edges).Distinct().ToArray();
        var paths = BuildSpanningTree(candidateEdges, random);
        int treePathCount = paths.Count;
        EnsureHexCoverage(hexEdges, paths, random);
        int coveragePathCount = paths.Count - treePathCount;
        AddExtraPaths(candidateEdges, paths, extraPathCount, random);

        logger.LogDebug("Generated {PathCount} land paths across {HexCount} hexes: {TreePathCount} in the spanning tree, {CoveragePathCount} for hex coverage, and {ExtraPathCount} extra.",
            paths.Count, hexEdges.Count, treePathCount, coveragePathCount, paths.Count - treePathCount - coveragePathCount);
        return paths.ToArray();
    }

    private static Dictionary<AxialCoords, IReadOnlyList<Path>> CollectHexEdges(IReadOnlyList<AxialCoords> continent)
    {
        var hexEdges = new Dictionary<AxialCoords, IReadOnlyList<Path>>();
        foreach (var hex in continent.Distinct())
        {
            if (!hex.IsHex()) throw new ArgumentException("Expected a hex center.", nameof(continent));

            var corners = hex.HexGetNeighbors();
            var edges = new List<Path>(6);
            for (int i = 0; i < corners.Count; i++)
            {
                for (int j = i + 1; j < corners.Count; j++)
                {
                    var from = corners[i];
                    var to = corners[j];
                    int dq = from.q - to.q;
                    int dr = from.r - to.r;
                    int distance = (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(dq + dr)) / 2;
                    if (distance == 1) edges.Add(new Path(from, to));
                }
            }

            hexEdges.Add(hex, edges);
        }

        return hexEdges;
    }

    private static HashSet<Path> BuildSpanningTree(IReadOnlyCollection<Path> candidateEdges, Random random)
    {
        var shuffled = candidateEdges.ToArray();
        random.Shuffle(shuffled);

        var groups = new Dictionary<AxialCoords, AxialCoords>();
        foreach (var edge in shuffled)
        {
            groups.TryAdd(edge.from, edge.from);
            groups.TryAdd(edge.to, edge.to);
        }

        var paths = new HashSet<Path>();
        foreach (var edge in shuffled)
        {
            var fromGroup = FindGroup(edge.from);
            var toGroup = FindGroup(edge.to);
            if (fromGroup.Equals(toGroup)) continue;

            groups[fromGroup] = toGroup;
            paths.Add(edge);
            if (paths.Count == groups.Count - 1)
                return paths;
        }
        
        throw new ArgumentException("The continent's hex edges form a disconnected graph.");

        AxialCoords FindGroup(AxialCoords node)
        {
            while (!groups[node].Equals(node))
            {
                groups[node] = groups[groups[node]];
                node = groups[node];
            }

            return node;
        }
    }

    private static void EnsureHexCoverage(IReadOnlyDictionary<AxialCoords, IReadOnlyList<Path>> hexEdges, HashSet<Path> paths, Random random)
    {
        foreach (var edges in hexEdges.Values)
        {
            if (!edges.Any(paths.Contains)) paths.Add(edges[random.Next(edges.Count)]);
        }
    }

    private static void AddExtraPaths(
        IReadOnlyCollection<Path> candidateEdges, HashSet<Path> paths, int extraPathCount, Random random)
    {
        if (extraPathCount == 0) return;

        var unused = candidateEdges.Where(edge => !paths.Contains(edge)).ToArray();
        random.Shuffle(unused);
        int count = Math.Min(extraPathCount, unused.Length);
        for (int i = 0; i < count; i++)
        {
            paths.Add(unused[i]);
        }
    }
}
