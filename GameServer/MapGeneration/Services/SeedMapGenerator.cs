namespace GameServer;

public class SeedMapGenerator
{
    private const int CandidateCount = 256;
    private const int MaxCrossingSteps = 5;

    private sealed record Layout(GeneratedMap Map, IReadOnlyList<IReadOnlyList<AxialCoords>> Continents);

    public GeneratedMap Generate(MapGenerationOptions options, int seed, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        if (!float.IsFinite(options.SeaLevel) || options.SeaLevel <= 0 || options.SeaLevel >= 1)
            throw new ArgumentOutOfRangeException(nameof(options), "Seed maps require a sea level strictly between 0 and 1.");

        GeneratedMap? best = null;
        double bestScore = double.PositiveInfinity;
        int bestCount = 0;
        int bestLongestCrossing = 0;
        long bestMovedCellSteps = 0;
        var generator = new MapGenerator();

        for (int attempt = 0; attempt < CandidateCount; attempt++)
        {
            int candidateSeed = unchecked(seed + attempt * (int)0x9E3779B9u);
            var map = generator.Generate(options, new NoiseHeightSource(candidateSeed, logger), logger);
            var regions = ContinentPathGenerator.FindLandRegions(map);
            int minimumSize = Math.Max(3, map.Cells.Count / 50);
            var continents = regions.Where(region => region.Count >= minimumSize).ToList();
            int landCount = continents.Sum(region => region.Count);
            if (landCount == 0)
            {
                logger.LogDebug("Candidate {Attempt} for seed {Seed}: no substantial land.", attempt, seed);
                continue;
            }

            double landFraction = (double)landCount / map.Cells.Count;
            double largestFraction = (double)continents.Max(region => region.Count) / landCount;
            double terrainScore = Math.Abs(continents.Count - 4) * 10
                                + Math.Abs(landFraction - 0.35) * 8
                                + Math.Max(0, largestFraction - 0.45) * 24
                                + Math.Max(0, 0.25 - landFraction) * 80;
            // Moving continents does not change the terrain score, and the remaining terms only add to it.
            if (terrainScore >= bestScore) continue;

            var land = continents.SelectMany(region => region).ToHashSet();
            var filteredMap = new GeneratedMap(map.Cells.Select(cell =>
                !map.IsWater(cell) && !land.Contains(cell.Coords)
                    ? cell with { Elevation = 0f }
                    : cell), map.SeaLevel);

            var fitted = FitCrossings(new Layout(filteredMap, continents));
            if (fitted is not { } result)
            {
                logger.LogDebug("Candidate {Attempt} for seed {Seed}: could not connect its continents within {MaxCrossingSteps} steps per crossing.",
                    attempt, seed, MaxCrossingSteps);
                continue;
            }

            double score = terrainScore + 0.1 * result.AverageCrossing + (double)result.MovedCellSteps / landCount;
            logger.LogDebug(
                "Candidate {Attempt} for seed {Seed} (noise seed {CandidateSeed}): score {Score:F3}, continents {ContinentCount}, land {LandFraction:P1}, largest continent {LargestFraction:P1} of land, longest crossing {LongestCrossing}, moved hex steps {MovedCellSteps}.",
                attempt, seed, candidateSeed, score, continents.Count, landFraction, largestFraction, result.LongestCrossing, result.MovedCellSteps);
            if (score >= bestScore) continue;

            best = result.Map;
            bestScore = score;
            bestCount = continents.Count;
            bestLongestCrossing = result.LongestCrossing;
            bestMovedCellSteps = result.MovedCellSteps;
        }

        if (best is null)
        {
            logger.LogError("No suitable map found for seed {Seed} after {CandidateCount} candidates.", seed, CandidateCount);
            throw new InvalidOperationException("No map with substantial land and the required crossing lengths was generated.");
        }

        logger.LogInformation("Seed {Seed}: selected a map with {ContinentCount} landmasses and score {Score:F3} from {CandidateCount} candidates. Longest crossing: {LongestCrossing} steps; moved hex steps: {MovedCellSteps}.",
            seed, bestCount, bestScore, CandidateCount, bestLongestCrossing, bestMovedCellSteps);
        return new GeneratedMap(best.Cells, best.SeaLevel, MaxCrossingSteps);
    }

    private static (GeneratedMap Map, long MovedCellSteps, double AverageCrossing, int LongestCrossing)? FitCrossings(Layout layout)
    {
        var crossings = ContinentPathGenerator.FindSpanningTree(layout.Map, layout.Continents, out _);
        if (crossings.Count != layout.Continents.Count - 1) return null;
        if (crossings.Count == 0) return (layout.Map, 0, 0, 0);

        long movedCellSteps = 0;
        var cost = CrossingCost(crossings);
        while (cost.Excess > 0)
        {
            Layout? bestMove = null;
            var bestCrossings = crossings;
            var bestCost = cost;
            int bestMoveSize = 0;
            var cells = layout.Map.Cells.ToDictionary(cell => cell.Coords);
            var allLand = layout.Continents.SelectMany(continent => continent).ToHashSet();
            var movableContinents = crossings.Where(crossing => crossing.Steps.Count > MaxCrossingSteps)
                .SelectMany(crossing => new[] { crossing.FromContinent, crossing.ToContinent })
                .Distinct()
                .OrderBy(index => layout.Continents[index].Count);

            foreach (int index in movableContinents)
            {
                var continent = layout.Continents[index];
                var otherLand = allLand.Except(continent).ToHashSet();
                var blocked = new HashSet<AxialCoords>(otherLand);
                foreach (var hex in otherLand)
                {
                    foreach (var (q, r) in ContinentPathGenerator.HexNeighborOffsets)
                        blocked.Add(new AxialCoords(hex.q + q, hex.r + r));
                }

                foreach (var (q, r) in ContinentPathGenerator.HexNeighborOffsets)
                {
                    var moved = MoveContinent(layout, index, q, r, cells, blocked);
                    if (moved is null) continue;

                    var movedCrossings = ContinentPathGenerator.FindSpanningTree(moved.Map, moved.Continents, out _);
                    if (movedCrossings.Count != crossings.Count) continue;

                    var movedCost = CrossingCost(movedCrossings);
                    if (movedCost.CompareTo(bestCost) >= 0) continue;

                    bestMove = moved;
                    bestCrossings = movedCrossings;
                    bestCost = movedCost;
                    bestMoveSize = continent.Count;
                }
            }

            if (bestMove is null) return null;
            layout = bestMove;
            crossings = bestCrossings;
            cost = bestCost;
            movedCellSteps += bestMoveSize;
        }

        return (layout.Map, movedCellSteps, (double)cost.Length / crossings.Count, crossings.Max(crossing => crossing.Steps.Count));
    }

    private static (int Excess, int Length) CrossingCost(IReadOnlyList<ContinentPathGenerator.Crossing> crossings)
    {
        return (crossings.Sum(crossing => Math.Max(0, crossing.Steps.Count - MaxCrossingSteps)),
            crossings.Sum(crossing => crossing.Steps.Count));
    }

    private static Layout? MoveContinent(Layout layout, int index, int q, int r,
        IReadOnlyDictionary<AxialCoords, GeneratedCell> cells, HashSet<AxialCoords> blocked)
    {
        var continent = layout.Continents[index];
        var translated = new List<AxialCoords>(continent.Count);
        var elevations = new Dictionary<AxialCoords, float>(continent.Count);
        foreach (var hex in continent)
        {
            var target = new AxialCoords(hex.q + q, hex.r + r);
            if (!cells.ContainsKey(target) || blocked.Contains(target)) return null;

            translated.Add(target);
            elevations.Add(target, cells[hex].Elevation);
        }

        var oldLand = continent.ToHashSet();
        var map = new GeneratedMap(layout.Map.Cells.Select(cell =>
        {
            if (elevations.TryGetValue(cell.Coords, out float elevation)) return cell with { Elevation = elevation };
            return oldLand.Contains(cell.Coords) ? cell with { Elevation = 0f } : cell;
        }), layout.Map.SeaLevel);
        var continents = layout.Continents.ToArray();
        continents[index] = translated;
        return new Layout(map, continents);
    }
}
