namespace GameServer;

internal class TerrainGenerator
{
    private const float NoiseSamplePadding = 0.15f;
    private const int MoistureSeedOffset = 0x53A86C1D;
    private const double PreferredTerrainWeight = 2.5;
    private const double SameTerrainNeighborWeight = 0.33;
    private static readonly TerrainType[] TerrainTypes = Enum.GetValues<TerrainType>();

    public Dictionary<AxialCoords, TerrainType> Generate(GeneratedMap map, int seed, ILogger logger)
    {
        var land = map.Cells.Where(cell => !map.IsWater(cell)).ToArray();
        var terrains = new Dictionary<AxialCoords, TerrainType>(land.Length);
        if (land.Length == 0) return terrains;

        double minX = land.Min(cell => HorizontalPosition(cell.Coords));
        double maxX = land.Max(cell => HorizontalPosition(cell.Coords));
        int minY = land.Min(cell => cell.Coords.r);
        int maxY = land.Max(cell => cell.Coords.r);
        var moisture = new NoiseHeightSource(seed ^ MoistureSeedOffset, logger);
        var byElevation = land.Select(cell => (Cell: cell, Moisture: moisture.Sample(
                NormalizeForNoise(HorizontalPosition(cell.Coords), minX, maxX),
                NormalizeForNoise(cell.Coords.r, minY, maxY))))
            .OrderByDescending(tile => tile.Cell.Elevation)
            .ThenByDescending(tile => tile.Moisture)
            .ToArray();

        int mountainCount = land.Length / 4;
        int hillCount = land.Length / 4;
        for (int i = 0; i < mountainCount + hillCount; i++)
        {
            var terrain = i < mountainCount ? TerrainType.Mountains : TerrainType.Hills;
            terrains.Add(byElevation[i].Cell.Coords, terrain);
        }

        var lowlands = byElevation.Skip(mountainCount + hillCount)
            .OrderByDescending(tile => tile.Moisture).ToArray();
        int forestCount = lowlands.Length / 2;
        for (int i = 0; i < lowlands.Length; i++)
        {
            var terrain = i < forestCount ? TerrainType.Forest : TerrainType.Plains;
            terrains.Add(lowlands[i].Cell.Coords, terrain);
        }

        logger.LogDebug("Generated terrain for {LandCount} hexes: {MountainCount} mountains, {HillCount} hills, {ForestCount} forests, and {PlainsCount} plains.",
            land.Length, mountainCount, hillCount, forestCount, lowlands.Length - forestCount);
        return MixTerrain(terrains, new Random(seed));
    }

    private static Dictionary<AxialCoords, TerrainType> MixTerrain(IReadOnlyDictionary<AxialCoords, TerrainType> preferredTerrains, Random random)
    {
        var coords = preferredTerrains.Keys.ToArray();
        random.Shuffle(coords);
        var remaining = new int[TerrainTypes.Length];
        foreach (var terrain in preferredTerrains.Values) remaining[(int)terrain]++;

        var placed = new Dictionary<AxialCoords, TerrainType>(coords.Length);
        var weights = new double[TerrainTypes.Length];
        foreach (var cell in coords)
        {
            var preferred = preferredTerrains[cell];
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = remaining[i] * (TerrainTypes[i] == preferred ? PreferredTerrainWeight : 1);
            }

            foreach (var (q, r) in ContinentPathGenerator.HexNeighborOffsets)
            {
                var neighbor = new AxialCoords(cell.q + q, cell.r + r);
                if (placed.TryGetValue(neighbor, out var terrain))
                    weights[(int)terrain] *= SameTerrainNeighborWeight;
            }

            double totalWeight = 0;
            foreach (double weight in weights) totalWeight += weight;
            double pick = random.NextDouble() * totalWeight;
            TerrainType selected = preferred;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] == 0) continue;
                selected = TerrainTypes[i];
                pick -= weights[i];
                if (pick < 0) break;
            }

            placed.Add(cell, selected);
            remaining[(int)selected]--;
        }

        return placed;
    }

    private static double HorizontalPosition(AxialCoords coords) => coords.q + coords.r / 2.0;

    private static float NormalizeForNoise(double value, double min, double max)
    {
        if (Math.Abs(max - min) < 1e-7) return 0.5f;
        return NoiseSamplePadding + (1f - 2f * NoiseSamplePadding) * (float)((value - min) / (max - min));
    }
}
