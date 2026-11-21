namespace GameServer;

public class SeedMapGenerator
{
    private const int CandidateCount = 256;
    private static readonly (int Q, int R)[] Neighbors =
        [(1, 1), (2, -1), (1, -2), (-1, -1), (-2, 1), (-1, 2)];

    public GeneratedMap Generate(MapGenerationOptions options, int seed, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        if (!float.IsFinite(options.SeaLevel) || options.SeaLevel <= 0 || options.SeaLevel >= 1)
            throw new ArgumentOutOfRangeException(nameof(options), "Seed maps require a sea level strictly between 0 and 1.");

        GeneratedMap? best = null;
        double bestScore = double.PositiveInfinity;
        int bestCount = 0;
        var generator = new MapGenerator();

        for (int attempt = 0; attempt < CandidateCount; attempt++)
        {
            int candidateSeed = unchecked(seed + attempt * (int)0x9E3779B9u);
            var map = generator.Generate(options, new NoiseHeightSource(candidateSeed, logger), logger);
            var regions = FindLandRegions(map);
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
            double score = Math.Abs(continents.Count - 4) * 10
                         + Math.Abs(landFraction - 0.35) * 8
                         + Math.Max(0, largestFraction - 0.45) * 24
                         + Math.Max(0, 0.25 - landFraction) * 80;
            logger.LogDebug(
                "Candidate {Attempt} for seed {Seed} (noise seed {CandidateSeed}): score {Score:F3}, continents {ContinentCount}, land {LandFraction:P1}, largest continent {LargestFraction:P1} of land.",
                attempt, seed, candidateSeed, score, continents.Count, landFraction, largestFraction);
            if (score >= bestScore) continue;

            var land = continents.SelectMany(region => region).ToHashSet();
            best = new GeneratedMap(map.Cells.Select(cell =>
                !map.IsWater(cell) && !land.Contains(cell.Coords)
                    ? cell with { Elevation = 0f }
                    : cell), map.SeaLevel);
            bestScore = score;
            bestCount = continents.Count;
        }

        if (best is null)
        {
            logger.LogError("No substantial land found for seed {Seed} after {CandidateCount} candidates.", seed, CandidateCount);
            throw new InvalidOperationException("No substantial land was generated. Increase the map size or lower the sea level.");
        }

        logger.LogInformation("Seed {Seed}: selected a map with {ContinentCount} landmasses and score {Score:F3} from {CandidateCount} candidates.",
            seed, bestCount, bestScore, CandidateCount);
        return best;
    }

    private static List<List<AxialCoords>> FindLandRegions(GeneratedMap map)
    {
        var remaining = map.Cells.Where(cell => !map.IsWater(cell)).Select(cell => cell.Coords).ToHashSet();
        var regions = new List<List<AxialCoords>>();
        foreach (var cell in map.Cells)
        {
            if (!remaining.Remove(cell.Coords)) continue;
            var region = new List<AxialCoords> { cell.Coords };
            for (int i = 0; i < region.Count; i++)
            {
                foreach (var (q, r) in Neighbors)
                {
                    var neighbor = new AxialCoords(region[i].q + q, region[i].r + r);
                    if (remaining.Remove(neighbor)) region.Add(neighbor);
                }
            }
            regions.Add(region);
        }
        return regions;
    }
}
