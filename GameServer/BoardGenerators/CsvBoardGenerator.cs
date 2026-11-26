using System.Collections.Concurrent;

namespace GameServer;

public class CsvBoardGenerator
{
    private readonly string _mapsPath;
    private readonly ILogger<CsvBoardGenerator> _logger;
    private readonly ConcurrentDictionary<string, CachedMapData> _cache = new(StringComparer.OrdinalIgnoreCase);

    private sealed record CachedMapData(
        IReadOnlyDictionary<AxialCoords, Hex> Hexes,
        IReadOnlyDictionary<AxialCoords, Node> Nodes,
        IReadOnlyDictionary<Path, ExplorationPath> ExplorationPaths,
        IReadOnlySet<Path> BlockedPaths);

    public CsvBoardGenerator(ILogger<CsvBoardGenerator> logger)
    {
        _mapsPath = System.IO.Path.Combine(AppContext.BaseDirectory, "GameFiles", "Maps");
        _logger = logger;
    }

    public BoardState GenerateBoard(string mapName)
    {
        var cached = _cache.GetOrAdd(mapName, LoadAndParseMap);
        var copy = CloneCachedMap(cached);

        return new BoardState(
            new Dictionary<AxialCoords, Hex>(copy.Hexes),
            new Dictionary<AxialCoords, Node>(copy.Nodes),
            new Dictionary<Path, ExplorationPath>(copy.ExplorationPaths),
            new HashSet<Path>(copy.BlockedPaths));
    }

    private CachedMapData LoadAndParseMap(string mapName)
    {
        _logger.LogInformation("Generating board from CSV files in {MapsPath} for map {MapName}.", _mapsPath, mapName);

        var mapPath = System.IO.Path.Combine(_mapsPath, mapName);
        var hexesPath = System.IO.Path.Combine(mapPath, "hexes.csv");
        var nodesPath = System.IO.Path.Combine(mapPath, "nodes.csv");
        var pathsPath = System.IO.Path.Combine(mapPath, "paths.csv");

        using var hexesReader = new StreamReader(hexesPath);
        using var nodesReader = new StreamReader(nodesPath);
        using var pathsReader = new StreamReader(pathsPath);

        var hexes = ReadHexesCsv(hexesReader);
        var nodes = ReadNodesCsv(nodesReader);
        var path_data = ReadPathsCsv(pathsReader);
        var explorationPaths = path_data.Item1;
        var blockedPaths = path_data.Item2;

        _logger.LogInformation(
            "Map {MapName} cached: {HexCount} hexes, {NodeCount} nodes, {PathCount} paths.",
            mapName,
            hexes.Count,
            nodes.Count,
            explorationPaths.Count);

        return new CachedMapData(hexes, nodes, explorationPaths, blockedPaths);
    }

    private CachedMapData CloneCachedMap(CachedMapData cached)
    {
        var hexes = cached.Hexes.ToDictionary(
            pair => pair.Key,
            pair => new Hex(
                pair.Value.Coords,
                pair.Value.Terrain,
                pair.Value.NumberToken,
                pair.Value.Region));

        var nodes = cached.Nodes.ToDictionary(
            pair => pair.Key,
            pair => new Node(
                pair.Value.Coords,
                pair.Value.Tribe,
                pair.Value.IsStartSpace,
                pair.Value.CanSettle,
                pair.Value.CanSettle3p));

        var explorationPaths = cached.ExplorationPaths.ToDictionary(
            pair => new Path(pair.Key.from, pair.Key.to),
            pair => new ExplorationPath(
                pair.Value.ClothingRequirement,
                pair.Value.ShelterRequirement,
                Tribe.Asians));

        var blockedPaths = cached.BlockedPaths
            .Select(path => new Path(path.from, path.to))
            .ToHashSet();

        return new CachedMapData(
            hexes,
            nodes,
            explorationPaths,
            blockedPaths);
    }

    private static Dictionary<string, TerrainType> terrain_names = new Dictionary<string, TerrainType>
    {
        ["hills"] = TerrainType.Hills,
        ["plains"] = TerrainType.Plains,
        ["mountains"] = TerrainType.Mountains,
        ["forest"] = TerrainType.Forest
    };

    private static Dictionary<string, Region> region_names = new Dictionary<string, Region>
    {
        ["africa"] = Region.Africa,
        ["eurasia"] = Region.Eurasia,
        ["australia"] = Region.Australia,
        ["america"] = Region.America
    };

    private Dictionary<AxialCoords, Hex> ReadHexesCsv(StreamReader hexes_rd)
    {
        var colNames = hexes_rd.ReadLine()?.Split(',');
        if (colNames == null)
        {
            throw new InvalidOperationException("Hexes CSV file is empty or invalid.");
        }

        var dict = new Dictionary<string, int>();

        for (int i = 0; i < colNames.Length; i++)
        {
            dict[colNames[i].Trim()] = i;
        }

        var hexes = new Dictionary<AxialCoords, Hex>();

        while (!hexes_rd.EndOfStream)
        {
            var line = hexes_rd.ReadLine();
            var fields = line?.Split(',');
            if (fields == null)
            {
                throw new InvalidOperationException("Hexes CSV file is empty or invalid.");
            }

            int q = int.Parse(fields[dict["q"]].Trim());
            int r = int.Parse(fields[dict["r"]].Trim());
            AxialCoords coords = new AxialCoords(q, r);
            Region region = region_names[fields[dict["region"]].Trim()];
            int number = int.Parse(fields[dict["number"]].Trim());
            TerrainType terrain = terrain_names[fields[dict["terrain"]].Trim()];

            hexes.Add(coords, new Hex(coords, terrain, number, region));
        }

        return hexes;
    }

    private static Dictionary<string, Tribe> tribe_names = new Dictionary<string, Tribe>
    {
        ["Indo-Europeans"] = Tribe.Indo_Europeans,
        ["Asians"] = Tribe.Asians,
        ["Austronesians"] = Tribe.Austronesians,
        ["Americans"] = Tribe.Americans
    };

    private Dictionary<AxialCoords, Node> ReadNodesCsv(StreamReader node_rd)
    {
        var colNames = node_rd.ReadLine()?.Split(',');
        if (colNames == null)
        {
            throw new InvalidOperationException("Nodes CSV file is empty or invalid.");
        }

        var dict = new Dictionary<string, int>();

        for (int i = 0; i < colNames.Length; i++)
        {
            dict[colNames[i].Trim()] = i;
        }

        var nodes = new Dictionary<AxialCoords, Node>();

        while (!node_rd.EndOfStream)
        {
            var line = node_rd.ReadLine();
            var fields = line?.Split(',');
            if (fields == null)
            {
                throw new InvalidOperationException("Nodes CSV file is empty or invalid.");
            }

            int q = int.Parse(fields[dict["q"]].Trim());
            int r = int.Parse(fields[dict["r"]].Trim());
            AxialCoords coords = new AxialCoords(q, r);

            bool start = bool.Parse(fields[dict["start"]].Trim());
            bool settle = bool.Parse(fields[dict["settle"]].Trim());
            bool settle_3player = bool.Parse(fields[dict["settle_3player"]].Trim());

            Tribe? tribe = null;
            if (settle)
            {
                tribe = tribe_names[fields[dict["tribe"]].Trim()];
            }

            nodes.Add(coords, new Node(coords, tribe, start, settle, settle_3player));
        }

        return nodes;
    }

    private (Dictionary<Path, ExplorationPath>, HashSet<Path>) ReadPathsCsv(StreamReader path_rd)
    {
        var colNames = path_rd.ReadLine()?.Split(',');
        if (colNames == null)
        {
            throw new InvalidOperationException("Paths CSV file is empty or invalid.");
        }

        var dict = new Dictionary<string, int>();

        for (int i = 0; i < colNames.Length; i++)
        {
            dict[colNames[i].Trim()] = i;
        }

        Dictionary<Path, ExplorationPath> exploration_paths = new Dictionary<Path, ExplorationPath>();
        HashSet<Path> blocked_paths = new HashSet<Path>();

        while (!path_rd.EndOfStream)
        {
            var line = path_rd.ReadLine();
            var fields = line?.Split(',');
            if (fields == null)
            {
                throw new InvalidOperationException("Paths CSV file is empty or invalid.");
            }

            int from_q = int.Parse(fields[dict["from_q"]].Trim());
            int from_r = int.Parse(fields[dict["from_r"]].Trim());
            int to_q = int.Parse(fields[dict["to_q"]].Trim());
            int to_r = int.Parse(fields[dict["to_r"]].Trim());
            AxialCoords from_coords = new AxialCoords(from_q, from_r);
            AxialCoords to_coords = new AxialCoords(to_q, to_r);
            Path path = new Path(from_coords, to_coords);

            bool is_blocked = bool.Parse(fields[dict["no_path"]].Trim());
            if (is_blocked)
            {
                blocked_paths.Add(path);
                continue;
            }

            Tribe tribe = tribe_names[fields[dict["race"]].Trim()];
            int clothing = int.Parse(fields[dict["clothing"]].Trim());
            int shelter = int.Parse(fields[dict["shelter"]].Trim());

            exploration_paths.Add(path, new ExplorationPath(clothing, shelter, tribe));
        }

        return (exploration_paths, blocked_paths);
    }
}