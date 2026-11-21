using Microsoft.Extensions.Logging.Abstractions;

namespace GameServer;

public class BoardBuilder
{
    private const int HexesPerExtraLandPath = 4;

    public BoardState Build(GeneratedMap map, int seed = 0, ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        loggerFactory ??= NullLoggerFactory.Instance;

        var terrains = new TerrainGenerator().Generate(map, seed, loggerFactory.CreateLogger<TerrainGenerator>());
        var hexes = new Dictionary<AxialCoords, Hex>(terrains.Count);
        foreach (var cell in map.Cells)
        {
            if (terrains.TryGetValue(cell.Coords, out var terrain))
                hexes[cell.Coords] = new Hex(cell.Coords, terrain, -1, Region.Africa);
        }

        var continents = ContinentPathGenerator.FindLandRegions(map);
        var landGenerator = new LandPathGenerator();
        var landLogger = loggerFactory.CreateLogger<LandPathGenerator>();
        var random = new Random(seed);
        var landPaths = new HashSet<Path>();
        foreach (var continent in continents)
        {
            int extraPathCount = continent.Count / HexesPerExtraLandPath;
            landPaths.UnionWith(landGenerator.Generate(continent, extraPathCount, random, landLogger));
        }

        var seaGenerator = new ContinentPathGenerator(loggerFactory.CreateLogger<ContinentPathGenerator>());
        var seaPaths = seaGenerator.Generate(map, continents, new Random(seed)).ToHashSet();

        var nodes = new Dictionary<AxialCoords, Node>();
        foreach (var path in landPaths.Concat(seaPaths))
        {
            AddNode(path.from);
            AddNode(path.to);
        }

        return new BoardState(hexes, nodes, new Dictionary<Path, ExplorationPath>(), new HashSet<Path>(), landPaths, seaPaths);

        void AddNode(AxialCoords coords)
        {
            if (!nodes.ContainsKey(coords)) nodes.Add(coords, new Node(coords, null, false, false, false));
        }
    }
}
