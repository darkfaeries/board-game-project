namespace GameServer;

public class CsvBoardGenerator
{
    private readonly string _mapsPath;
    private readonly ILogger<CsvBoardGenerator> _logger;

    public CsvBoardGenerator(ILogger<CsvBoardGenerator> logger)
    {
        _mapsPath = System.IO.Path.Combine(AppContext.BaseDirectory, "GameFiles", "Maps");
        _logger = logger;
    }

    public BoardState GenerateBoard(string mapName)
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
    

        var board = new BoardState(hexes, nodes, explorationPaths, blockedPaths);

        _logger.LogInformation(
            "Board generated: {HexCount} hexes, {NodeCount} nodes, {PathCount} paths.",
            board.Hexes.Count,
            board.Nodes.Count,
            board.ExplorationPaths.Count);

        return board;
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