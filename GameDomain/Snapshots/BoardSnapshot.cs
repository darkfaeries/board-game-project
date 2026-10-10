using System.Text.Json.Serialization;

public sealed class BoardSnapshot
{
    public int Version { get; set; } = 1;
    public List<HexSnapshot> Hexes { get; set; } = new();
    public List<NodeSnapshot> Nodes { get; set; } = new();
    public List<ExplorationPathSnapshot> ExplorationPaths { get; set; } = new();
    public List<EdgeSnapshot> BlockedPaths { get; set; } = new();
    public CoordSnapshot? NeanderthalPosition { get; set; }
    public CoordSnapshot? SabertoothTigerPosition { get; set; }

    public static BoardSnapshot FromBoardState(BoardState board) => new()
    {
        Hexes = board.Hexes.Values.Select(h => new HexSnapshot
        {
            Coords = CoordSnapshot.From(h.Coords), Terrain = h.Terrain,
            NumberToken = h.NumberToken, Region = h.Region, IsDesertified = h.IsDesertified
        }).ToList(),
        Nodes = board.Nodes.Values.Select(n => new NodeSnapshot
        {
            Coords = CoordSnapshot.From(n.Coords), Tribe = n.Tribe, IsStartSpace = n.IsStartSpace,
            CanSettle = n.CanSettle, CanSettle3p = n.CanSettle3p, HasTribeCounter = n.HasTribeCounter
        }).ToList(),
        ExplorationPaths = board.ExplorationPaths.Select(p => new ExplorationPathSnapshot
        {
            From = CoordSnapshot.From(p.Key.from), To = CoordSnapshot.From(p.Key.to),
            ClothingRequirement = p.Value.ClothingRequirement, ShelterRequirement = p.Value.ShelterRequirement,
            Counter = CounterSnapshot.From(p.Value.ExplorationCounter)
        }).ToList(),
        BlockedPaths = board.BlockedPaths.Select(p => new EdgeSnapshot
        { From = CoordSnapshot.From(p.from), To = CoordSnapshot.From(p.to) }).ToList(),
        NeanderthalPosition = board.NeanderthalPosition == null ? null : CoordSnapshot.From(board.NeanderthalPosition),
        SabertoothTigerPosition = board.SabertoothTigerPosition == null ? null : CoordSnapshot.From(board.SabertoothTigerPosition)
    };

    public BoardState ToBoardState()
    {
        if (Version < 0 || Hexes == null || Nodes == null || ExplorationPaths == null ||
            BlockedPaths == null || Hexes.Count == 0 || Hexes.Count > 2000 || Nodes.Count > 6000 ||
            ExplorationPaths.Count > 12000 || BlockedPaths.Count > 12000)
            throw new InvalidOperationException("Invalid board size or snapshot version");
        
        var boardState = new BoardState();
        
        foreach (var h in Hexes)
        {
            if (!Enum.IsDefined(h.Terrain) || !Enum.IsDefined(h.Region) || h.NumberToken < 0 || h.NumberToken > 12)
                throw new InvalidOperationException("Invalid hex");

            var coords = h.Coords.ToCoords();
            boardState.Hexes.Add(
                coords, 
                new Hex(coords, h.Terrain, h.NumberToken, h.Region) { IsDesertified = h.IsDesertified }
            );
        }

        foreach (var n in Nodes)
        {
            if (n.Tribe.HasValue && !Enum.IsDefined(n.Tribe.Value)) 
                throw new InvalidOperationException("Invalid tribe");
            
            var coords = n.Coords.ToCoords();
            boardState.Nodes.Add(
                coords, 
                new Node(coords, n.Tribe, n.IsStartSpace, n.CanSettle, n.CanSettle3p) { HasTribeCounter = n.HasTribeCounter }
            );
        }

        var edges = new HashSet<(int, int, int, int)>();
        foreach (var p in ExplorationPaths)
        {
            if (p.ClothingRequirement < 0 || p.ShelterRequirement < 0) 
                throw new InvalidOperationException("Invalid path requirements");
            
            var path = ReadEdge(p, boardState, edges);
            boardState.ExplorationPaths.Add(path, new ExplorationPath(p.ClothingRequirement, p.ShelterRequirement, default) //Tribe in ExpPath not used
                { ExplorationCounter = p.Counter?.ToCounter() });
        }

        foreach (var p in BlockedPaths) 
            boardState.BlockedPaths.Add(ReadEdge(p, boardState, edges));

        boardState.NeanderthalPosition = NeanderthalPosition?.ToCoords();
        boardState.SabertoothTigerPosition = SabertoothTigerPosition?.ToCoords();

        return boardState;
    }

    private static Path ReadEdge(
        EdgeSnapshot p, 
        BoardState boardState, 
        HashSet<(int, int, int, int)> edges
    )
    {
        var from = p.From.ToCoords(); var to = p.To.ToCoords();
        if (from.Equals(to) || !boardState.Nodes.ContainsKey(from) || !boardState.Nodes.ContainsKey(to))
            throw new InvalidOperationException("Invalid path endpoints");
            
        var key = (from.q, from.r, to.q, to.r);
        if (!edges.Add(key)) 
            throw new InvalidOperationException("Duplicate path");
        
        return new Path(from, to);
    }
}

public sealed class CoordSnapshot
{
    public int Q { get; set; }
    public int R { get; set; }
    public static CoordSnapshot From(AxialCoords c) => new() { Q = c.q, R = c.r };
    public AxialCoords ToCoords()
    {
        if (Q < -1000 || Q > 1000 || R < -1000 || R > 1000) 
            throw new InvalidOperationException("Invalid coordinates");
        
        return new AxialCoords(Q, R);
    }
}

public sealed class HexSnapshot
{
    public CoordSnapshot Coords { get; set; } = new();
    public TerrainType Terrain { get; set; }
    public int NumberToken { get; set; }
    public Region Region { get; set; }
    public bool IsDesertified { get; set; }
}

public sealed class NodeSnapshot
{
    public CoordSnapshot Coords { get; set; } = new();
    public Tribe? Tribe { get; set; }
    public bool IsStartSpace { get; set; }
    public bool CanSettle { get; set; }
    public bool CanSettle3p { get; set; }
    public bool HasTribeCounter { get; set; }
}

public class EdgeSnapshot
{
    public CoordSnapshot From { get; set; } = new();
    public CoordSnapshot To { get; set; } = new();
}

public sealed class ExplorationPathSnapshot : EdgeSnapshot
{
    public int ClothingRequirement { get; set; }
    public int ShelterRequirement { get; set; }
    public CounterSnapshot? Counter { get; set; }
}

public sealed class CounterSnapshot
{
    public string Kind { get; set; } = "";
    public Guid Id { get; set; }
    public AdaptationColor Color { get; set; }
    public TerrainType Terrain { get; set; }

    public static CounterSnapshot? From(ExplorationCounter? c) => c switch
    {
        null => null,
        AdaptationCounter a => 
            new() { Kind = "adaptation", Id = a.Id, Color = a.Color },
        DesertificationCounter d => 
            new() { Kind = "desertification", Id = d.Id, Terrain = d.Terrain },
        EnemyExplorationCounter e => 
            new() { Kind = "enemy", Id = e.Id },
        _ => throw new InvalidOperationException("Unsupported counter")
    };

    public ExplorationCounter ToCounter() => Kind switch
    {
        "adaptation" when Enum.IsDefined(Color) => 
            new AdaptationCounter { Id = Id, Color = Color },
        "desertification" when Enum.IsDefined(Terrain) => 
            new DesertificationCounter { Id = Id, Terrain = Terrain },
        "enemy" => 
            new EnemyExplorationCounter { Id = Id },
        _ => throw new InvalidOperationException("Unsupported counter")
    };
}
