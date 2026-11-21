namespace GameServer;

public class ContinentPathGenerator
{
    private const int ExtraCrossingCount = 2;

    private static readonly (int Q, int R)[] CornerOffsets =
        [(1, 0), (0, 1), (-1, 1), (-1, 0), (0, -1), (1, -1)];

    private static readonly (int Q, int R)[] NeighborOffsets = [(1, 1), (2, -1), (1, -2), (-1, -1), (-2, 1), (-1, 2)];

    internal static ReadOnlySpan<(int Q, int R)> HexNeighborOffsets => NeighborOffsets;

    internal readonly record struct Crossing(int FromContinent, int ToContinent, IReadOnlyList<Path> Steps);
    private readonly ILogger<ContinentPathGenerator> _logger;

    public ContinentPathGenerator(ILogger<ContinentPathGenerator> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public IReadOnlyList<Path> Generate(GeneratedMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return Generate(map, FindLandRegions(map));
    }

    internal IReadOnlyList<Path> Generate(GeneratedMap map, IReadOnlyList<IReadOnlyList<AxialCoords>> continents, Random? random = null)
    {
        if (continents.Count < 2) return Array.Empty<Path>();

        var waterGraph = BuildWaterGraph(map);
        var crossings = FindCrossings(continents, waterGraph);
        var selected = BuildMinimumSpanningTree(continents.Count, crossings);
        if (selected.Count != continents.Count - 1)
        {
            _logger.LogError("Could only connect {SelectedCount} of {RequiredCount} continent pairs using {CandidateCount} water crossings.",
                selected.Count, continents.Count - 1, crossings.Count);
            throw new InvalidOperationException("The available water crossings do not connect all continents.");
        }

        var paths = selected.SelectMany(crossing => crossing.Steps).ToHashSet();
        int extraCrossings = 0;
        if (random is not null && map.MaxSeaCrossingSteps is { } maxSteps)
        {
            extraCrossings = AddCrossings(crossings.Except(selected), ExtraCrossingCount, maxSteps, random);
            if (extraCrossings < ExtraCrossingCount)
            {
                var alternatives = FindCrossings(continents, waterGraph, paths);
                extraCrossings += AddCrossings(alternatives, ExtraCrossingCount - extraCrossings, maxSteps, random);
            }
        }

        _logger.LogInformation("Connected {ContinentCount} continents using {SelectedCount} spanning and {ExtraCount} optional water crossings ({PathCount} unique steps).",
            continents.Count, selected.Count, extraCrossings, paths.Count);
        return paths.ToArray();

        int AddCrossings(IEnumerable<Crossing> candidates, int count, int maxSteps, Random random)
        {
            var eligible = candidates.Where(crossing => crossing.Steps.Count <= maxSteps).ToArray();
            random.Shuffle(eligible);
            int added = 0;
            foreach (var crossing in eligible)
            {
                if (crossing.Steps.All(paths.Contains)) continue;
                paths.UnionWith(crossing.Steps);
                if (++added == count) break;
            }
            return added;
        }
    }

    internal static IReadOnlyList<Crossing> FindSpanningTree(
        GeneratedMap map, IReadOnlyList<IReadOnlyList<AxialCoords>> continents, out int candidateCount)
    {
        candidateCount = 0;
        if (continents.Count < 2) return Array.Empty<Crossing>();

        var crossings = FindCrossings(continents, BuildWaterGraph(map));
        candidateCount = crossings.Count;
        return BuildMinimumSpanningTree(continents.Count, crossings);
    }

    internal static IReadOnlyList<IReadOnlyList<AxialCoords>> FindLandRegions(GeneratedMap map)
    {
        var remaining = map.Cells.Where(cell => !map.IsWater(cell)).Select(cell => cell.Coords).ToHashSet();
        var regions = new List<IReadOnlyList<AxialCoords>>();
        var queue = new Queue<AxialCoords>();

        foreach (var cell in map.Cells)
        {
            if (!remaining.Remove(cell.Coords)) continue;

            var region = new List<AxialCoords>();
            queue.Enqueue(cell.Coords);
            while (queue.TryDequeue(out var current))
            {
                region.Add(current);
                foreach (var (q, r) in NeighborOffsets)
                {
                    var neighbor = new AxialCoords(current.q + q, current.r + r);
                    if (remaining.Remove(neighbor)) queue.Enqueue(neighbor);
                }
            }

            regions.Add(region);
        }

        return regions;
    }

    private static Dictionary<AxialCoords, List<AxialCoords>> BuildWaterGraph(GeneratedMap map)
    {
        var graph = new Dictionary<AxialCoords, List<AxialCoords>>();
        foreach (var cell in map.Cells)
        {
            if (!map.IsWater(cell)) continue;

            var center = cell.Coords;
            var (lastQ, lastR) = CornerOffsets.Last();
            var previous = new AxialCoords(center.q + lastQ, center.r + lastR);
            foreach (var (q, r) in CornerOffsets)
            {
                var corner = new AxialCoords(center.q + q, center.r + r);
                AddNeighbor(previous, corner);
                AddNeighbor(corner, previous);
                previous = corner;
            }
        }

        return graph;

        void AddNeighbor(AxialCoords from, AxialCoords to)
        {
            if (!graph.TryGetValue(from, out var neighbors))
            {
                neighbors = new List<AxialCoords>();
                graph.Add(from, neighbors);
            }

            if (!neighbors.Contains(to)) neighbors.Add(to);
        }
    }

    private static IReadOnlyList<Crossing> FindCrossings(IReadOnlyList<IReadOnlyList<AxialCoords>> continents,
        IReadOnlyDictionary<AxialCoords, List<AxialCoords>> waterGraph,
        IReadOnlySet<Path>? excludedPaths = null)
    {
        var coasts = new List<AxialCoords[]>(continents.Count);
        var coastOwners = new Dictionary<AxialCoords, int>();
        for (int continent = 0; continent < continents.Count; continent++)
        {
            var coast = continents[continent]
                .SelectMany(hex => hex.HexGetNeighbors())
                .Where(waterGraph.ContainsKey)
                .Distinct()
                .ToArray();
            coasts.Add(coast);
            foreach (var node in coast) 
                coastOwners.Add(node, continent);
        }

        var crossings = new List<Crossing>();
        for (int source = 0; source < coasts.Count - 1; source++)
        {
            var queue = new Queue<AxialCoords>(coasts[source]);
            var previous = coasts[source].ToDictionary(node => node, _ => (AxialCoords?)null);
            var reachedContinents = new HashSet<int>();

            while (queue.TryDequeue(out var current))
            {
                foreach (var neighbor in waterGraph[current])
                {
                    if (excludedPaths is not null && excludedPaths.Contains(new Path(current, neighbor))) continue;
                    if (!previous.TryAdd(neighbor, current)) continue;

                    if (coastOwners.TryGetValue(neighbor, out int target))
                    {
                        if (target > source && reachedContinents.Add(target))
                        {
                            crossings.Add(new Crossing(source, target, RestoreSteps(neighbor, previous)));
                        }
                        continue;
                    }

                    queue.Enqueue(neighbor);
                }
            }
        }

        return crossings;
    }

    private static IReadOnlyList<Path> RestoreSteps(AxialCoords destination, IReadOnlyDictionary<AxialCoords, AxialCoords?> previous)
    {
        var steps = new List<Path>();
        for (var node = destination; previous[node] is { } parent; node = parent)
        {
            steps.Add(new Path(parent, node));
        }

        steps.Reverse();
        return steps;
    }

    private static IReadOnlyList<Crossing> BuildMinimumSpanningTree(int continentCount, IReadOnlyList<Crossing> crossings)
    {
        var groups = Enumerable.Range(0, continentCount).ToArray();
        var selected = new List<Crossing>(continentCount - 1);
        foreach (var crossing in crossings.OrderBy(crossing => crossing.Steps.Count))
        {
            int fromGroup = FindGroup(crossing.FromContinent);
            int toGroup = FindGroup(crossing.ToContinent);
            if (fromGroup == toGroup) continue;

            groups[fromGroup] = toGroup;
            selected.Add(crossing);
            if (selected.Count == continentCount - 1) break;
        }

        return selected;

        int FindGroup(int continent)
        {
            while (groups[continent] != continent)
            {
                groups[continent] = groups[groups[continent]];
                continent = groups[continent];
            }

            return continent;
        }
    }
}
